using System.Text.Json;
using System.Text.Json.Serialization;
using BinderPokedex.Domain;

namespace BinderPokedex.Services;

/// <summary>
/// Conteúdo do arquivo da coleção no gist. <see cref="Revision"/> aumenta a cada gravação e serve
/// para ignorar uma leitura atrasada do GitHub (mais antiga do que a última que este aparelho gravou).
/// </summary>
public sealed record CollectionFile(int Revision, DateTimeOffset UpdatedAt, IReadOnlyDictionary<int, CardRarity> Cards)
{
    public static CollectionFile Empty { get; } = new(0, DateTimeOffset.MinValue, new Dictionary<int, CardRarity>());

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(
        new Dto(Revision, UpdatedAt, new SortedDictionary<int, CardRarity>(Cards.ToDictionary())), Options);

    public static CollectionFile FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        var dto = JsonSerializer.Deserialize<Dto>(json, Options);
        return dto is null
            ? Empty
            : new CollectionFile(dto.Revision, dto.UpdatedAt,
                (dto.Cards ?? new SortedDictionary<int, CardRarity>()).Where(kv => BinderLayout.Fits(kv.Key)).ToDictionary());
    }

    public CollectionFile With(int pokemonId, CardRarity? rarity, DateTimeOffset now)
    {
        var cards = Cards.ToDictionary();
        if (rarity is { } r) cards[pokemonId] = r; else cards.Remove(pokemonId);
        return new CollectionFile(Revision + 1, now, cards);
    }

    private sealed record Dto(int Revision, DateTimeOffset UpdatedAt, SortedDictionary<int, CardRarity>? Cards);
}

/// <summary>Onde a coleção fica de verdade (o gist).</summary>
public interface ICollectionRemote
{
    bool IsConfigured { get; }
    Task<CollectionFile> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CollectionFile file, CancellationToken ct = default);
}

/// <summary>Cópia guardada no aparelho, para abrir na hora e funcionar sem internet.</summary>
public interface ICollectionCache
{
    (CollectionFile File, DateTimeOffset SyncedAt)? Load();
    void Save(CollectionFile file, DateTimeOffset syncedAt);
}

/// <summary>Token inválido ou sem permissão de gist.</summary>
public sealed class RemoteAuthException(string message) : Exception(message);
