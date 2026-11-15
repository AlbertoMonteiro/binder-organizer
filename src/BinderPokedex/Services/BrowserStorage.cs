using Microsoft.JSInterop;

namespace BinderPokedex.Services;

/// <summary>localStorage do navegador (síncrono no WebAssembly).</summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    private IJSInProcessRuntime Js => (IJSInProcessRuntime)js;

    public string? Get(string key)
    {
        try { return Js.Invoke<string?>("localStorage.getItem", key); }
        catch (JSException) { return null; }
    }

    public void Set(string key, string? value)
    {
        try
        {
            if (value is null) Js.InvokeVoid("localStorage.removeItem", key);
            else Js.InvokeVoid("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
        }
    }
}

public sealed class BrowserCollectionCache(BrowserStorage storage) : ICollectionCache
{
    private const string FileKey = "binder.collection";
    private const string SyncedKey = "binder.collection.synced";

    public (CollectionFile File, DateTimeOffset SyncedAt)? Load()
    {
        try
        {
            var json = storage.Get(FileKey);
            if (json is null) return null;
            var synced = DateTimeOffset.TryParse(storage.Get(SyncedKey), out var s) ? s : DateTimeOffset.MinValue;
            return (CollectionFile.FromJson(json), synced);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public void Save(CollectionFile file, DateTimeOffset syncedAt)
    {
        storage.Set(FileKey, file.ToJson());
        storage.Set(SyncedKey, syncedAt.ToString("O"));
    }
}

/// <summary>Token do GitHub e id do gist, guardados só neste aparelho.</summary>
public sealed class GistSettings(BrowserStorage storage)
{
    private const string TokenKey = "binder.github.token";
    private const string GistKey = "binder.github.gist";

    public string? Token => storage.Get(TokenKey);
    public string? GistId => storage.Get(GistKey);

    public void SetToken(string? token)
    {
        storage.Set(TokenKey, string.IsNullOrWhiteSpace(token) ? null : token.Trim());
        storage.Set(GistKey, null); // o próximo acesso procura o gist da conta desse token
    }

    public void SetGistId(string? id) => storage.Set(GistKey, id);
}
