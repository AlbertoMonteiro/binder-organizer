using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BinderPokedex.Services;

/// <summary>
/// Guarda a coleção num gist secreto do GitHub. O gist é achado pelo nome do arquivo
/// (ou criado na primeira vez), então em cada aparelho basta colar o token.
/// </summary>
public sealed class GistCollectionRemote(HttpClient http, GistSettings settings) : ICollectionRemote
{
    public const string FileName = "binder-pokedex.json";
    private const string Description = "Binder Pokédex — coleção (gerado pelo app)";

    public bool IsConfigured => settings.Token is { Length: > 0 };

    public async Task<CollectionFile> LoadAsync(CancellationToken ct = default)
    {
        var id = await EnsureGistAsync(ct);
        using var response = await SendAsync(HttpMethod.Get, $"gists/{id}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            settings.SetGistId(null); // apagado no GitHub: procura ou cria de novo na próxima vez
            throw new HttpRequestException("O gist da coleção não foi encontrado.");
        }
        await EnsureSuccessAsync(response);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.GetProperty("files").TryGetProperty(FileName, out var file)) return CollectionFile.Empty;

        // Arquivos grandes vêm truncados na API; a coleção tem poucos KB, mas por garantia busca o conteúdo bruto.
        if (file.TryGetProperty("truncated", out var truncated) && truncated.GetBoolean())
        {
            var raw = file.GetProperty("raw_url").GetString()!;
            return CollectionFile.FromJson(await http.GetStringAsync(raw, ct));
        }
        return CollectionFile.FromJson(file.GetProperty("content").GetString());
    }

    public async Task SaveAsync(CollectionFile collection, CancellationToken ct = default)
    {
        var id = await EnsureGistAsync(ct);
        using var response = await SendAsync(HttpMethod.Patch, $"gists/{id}", FilesPayload(collection), ct);
        await EnsureSuccessAsync(response);
    }

    private async Task<string> EnsureGistAsync(CancellationToken ct)
    {
        if (settings.GistId is { Length: > 0 } known) return known;

        for (var page = 1; page <= 10; page++)
        {
            using var list = await SendAsync(HttpMethod.Get, $"gists?per_page=100&page={page}", null, ct);
            await EnsureSuccessAsync(list);
            using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetArrayLength() == 0) break;

            foreach (var gist in doc.RootElement.EnumerateArray())
            {
                if (gist.GetProperty("files").TryGetProperty(FileName, out _))
                {
                    var found = gist.GetProperty("id").GetString()!;
                    settings.SetGistId(found);
                    return found;
                }
            }
        }

        var body = FilesPayload(CollectionFile.Empty);
        body["description"] = Description;
        body["public"] = false;
        using var created = await SendAsync(HttpMethod.Post, "gists", body, ct);
        await EnsureSuccessAsync(created);
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var id = createdDoc.RootElement.GetProperty("id").GetString()!;
        settings.SetGistId(id);
        return id;
    }

    private static Dictionary<string, object> FilesPayload(CollectionFile collection) => new()
    {
        ["files"] = new Dictionary<string, object> { [FileName] = new { content = collection.ToJson() } },
    };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.SetBrowserRequestCache(BrowserRequestCache.NoStore); // sempre a versão mais nova do gist
        if (body is not null) request.Content = JsonContent.Create(body);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            return await http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException("O GitHub demorou demais para responder.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new RemoteAuthException(response.StatusCode == HttpStatusCode.Unauthorized
                ? "O token do GitHub é inválido ou expirou."
                : "O token do GitHub não tem permissão de gist.");
        }
        var detail = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"GitHub respondeu {(int)response.StatusCode}: {detail[..Math.Min(detail.Length, 200)]}");
    }
}
