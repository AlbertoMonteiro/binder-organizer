using BinderPokedex.Domain;
using BinderPokedex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BinderPokedex.Services;

public sealed record GenerationProgress(GenerationInfo Generation, int Collected, int Total)
{
    public double Percent => Total == 0 ? 0 : Collected * 100.0 / Total;
}

/// <summary>
/// Guarda quais bolsos já têm carta e de que tipo. É singleton: todos os aparelhos conectados
/// (PC e celular) compartilham o mesmo estado e recebem <see cref="Changed"/> quando algo muda.
/// </summary>
public sealed class CollectionService(IDbContextFactory<BinderDbContext> dbFactory, PokedexService pokedex)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<int, CardRarity>? _collected;

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await UpgradeSchemaAsync(db);
        _collected = await db.Collected.ToDictionaryAsync(c => c.PokemonId, c => c.Rarity);
    }

    /// <summary>Bancos criados antes do tipo da carta não têm a coluna Rarity: adiciona como Normal.</summary>
    private static async Task UpgradeSchemaAsync(BinderDbContext db)
    {
        var columns = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('collected')")
            .ToListAsync();
        if (!columns.Contains(nameof(CollectedSlot.Rarity)))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE collected ADD COLUMN Rarity INTEGER NOT NULL DEFAULT 0");
        }
    }

    private Dictionary<int, CardRarity> Collected =>
        _collected ?? throw new InvalidOperationException("CollectionService não inicializado.");

    public bool Has(int pokemonId) => Collected.ContainsKey(pokemonId);

    public CardRarity? RarityOf(int pokemonId) =>
        Collected.TryGetValue(pokemonId, out var rarity) ? rarity : null;

    public int CollectedCount => Collected.Count;

    /// <summary>Coloca a carta com o tipo informado (ou troca o tipo); null tira do binder.</summary>
    public async Task SetAsync(int pokemonId, CardRarity? rarity)
    {
        if (!BinderLayout.Fits(pokemonId)) throw new ArgumentOutOfRangeException(nameof(pokemonId));

        await _lock.WaitAsync();
        try
        {
            if (RarityOf(pokemonId) == rarity) return;

            await using var db = await dbFactory.CreateDbContextAsync();
            if (rarity is not { } r)
            {
                await db.Collected.Where(c => c.PokemonId == pokemonId).ExecuteDeleteAsync();
            }
            else if (Has(pokemonId))
            {
                await db.Collected.Where(c => c.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.Rarity, r));
            }
            else
            {
                db.Collected.Add(new CollectedSlot { PokemonId = pokemonId, CollectedAt = DateTimeOffset.Now, Rarity = r });
                await db.SaveChangesAsync();
            }

            // Copia para que leitores em outros circuitos nunca vejam a coleção sendo alterada.
            var copy = new Dictionary<int, CardRarity>(Collected);
            if (rarity is { } value) copy[pokemonId] = value; else copy.Remove(pokemonId);
            _collected = copy;
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>Marca como carta normal (mantém o tipo se já estava colocada) ou tira do binder.</summary>
    public Task SetAsync(int pokemonId, bool collected) =>
        collected
            ? (Has(pokemonId) ? Task.CompletedTask : SetAsync(pokemonId, CardRarity.Normal))
            : SetAsync(pokemonId, (CardRarity?)null);

    public Task ToggleAsync(int pokemonId) => SetAsync(pokemonId, !Has(pokemonId));

    public IReadOnlyDictionary<CardRarity, int> CountByRarity()
    {
        var snapshot = Collected;
        return CardRarities.All.ToDictionary(r => r, r => snapshot.Values.Count(v => v == r));
    }

    public IReadOnlyList<GenerationProgress> ProgressByGeneration()
    {
        var snapshot = Collected;
        return Generations.All
            .Select(g =>
            {
                var ids = Enumerable.Range(g.FirstId, g.Count).Where(BinderLayout.Fits).ToList();
                return new GenerationProgress(g, ids.Count(snapshot.ContainsKey), ids.Count);
            })
            .ToList();
    }

    public IEnumerable<Pokemon> Missing(int? generation = null)
    {
        var snapshot = Collected;
        return pokedex.InBinder
            .Where(p => !snapshot.ContainsKey(p.Id))
            .Where(p => generation is null || p.Generation == generation);
    }
}
