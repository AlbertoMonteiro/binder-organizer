// Gera src/BinderPokedex/Data/pokemon.json a partir da PokeAPI.
// Uso: dotnet run --project tools/PokedexDataGen -- [caminho-de-saida]
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

const int Total = 1025;
var output = args.Length > 0
    ? args[0]
    : Path.Combine("src", "BinderPokedex", "Data", "pokemon.json");

using var http = new HttpClient { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };
http.DefaultRequestHeaders.UserAgent.ParseAdd("binder-pokedex-datagen/1.0");

var results = new ConcurrentDictionary<int, PokemonEntry>();
var done = 0;

await Parallel.ForEachAsync(Enumerable.Range(1, Total), new ParallelOptions { MaxDegreeOfParallelism = 8 },
    async (id, ct) =>
    {
        var species = await GetJson($"pokemon-species/{id}", ct);
        var pokemon = await GetJson($"pokemon/{id}", ct);

        var name = species["names"]!.AsArray()
            .First(n => n!["language"]!["name"]!.GetValue<string>() == "en")!["name"]!.GetValue<string>();
        var generation = RomanToInt(species["generation"]!["name"]!.GetValue<string>().Split('-')[1]);
        var types = pokemon["types"]!.AsArray()
            .OrderBy(t => t!["slot"]!.GetValue<int>())
            .Select(t => t!["type"]!["name"]!.GetValue<string>())
            .ToArray();

        results[id] = new PokemonEntry(id, name, generation, types);
        var n = Interlocked.Increment(ref done);
        if (n % 100 == 0) Console.WriteLine($"{n}/{Total}");
    });

var list = results.Values.OrderBy(p => p.Id).ToList();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(list, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}));
Console.WriteLine($"Gravado {list.Count} Pokémon em {Path.GetFullPath(output)}");

async Task<JsonNode> GetJson(string path, CancellationToken ct)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await using var stream = await http.GetStreamAsync(path, ct);
            return (await JsonNode.ParseAsync(stream, cancellationToken: ct))!;
        }
        catch (HttpRequestException) when (attempt < 5)
        {
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }
    }
}

static int RomanToInt(string roman) => roman.ToLowerInvariant() switch
{
    "i" => 1, "ii" => 2, "iii" => 3, "iv" => 4, "v" => 5,
    "vi" => 6, "vii" => 7, "viii" => 8, "ix" => 9,
    var other => throw new InvalidOperationException($"Geração desconhecida: {other}"),
};

record PokemonEntry(int Id, string Name, int Generation, string[] Types);
