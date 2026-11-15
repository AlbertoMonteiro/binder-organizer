using BinderPokedex.Domain;
using BinderPokedex.Persistence;
using BinderPokedex.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BinderPokedex.Tests;

public sealed class CollectionServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"binder-test-{Guid.NewGuid():N}.db");
    private readonly PokedexService _pokedex = new();

    private async Task<CollectionService> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<BinderDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        var service = new CollectionService(new Factory(options), _pokedex);
        await service.InitializeAsync();
        return service;
    }

    [Fact]
    public async Task Marca_troca_tipo_e_desmarca()
    {
        var service = await CreateAsync();

        await service.SetAsync(25, CardRarity.Foil);
        Assert.Equal(CardRarity.Foil, service.RarityOf(25));

        await service.SetAsync(25, CardRarity.ArteRara);
        Assert.Equal(CardRarity.ArteRara, service.RarityOf(25));
        Assert.Equal(1, service.CollectedCount);

        await service.SetAsync(25, (CardRarity?)null);
        Assert.Null(service.RarityOf(25));
        Assert.False(service.Has(25));
    }

    [Fact]
    public async Task Marcar_como_colocado_usa_normal_e_preserva_tipo_existente()
    {
        var service = await CreateAsync();

        await service.SetAsync(1, true);
        Assert.Equal(CardRarity.Normal, service.RarityOf(1));

        await service.SetAsync(4, CardRarity.UltraRara);
        await service.SetAsync(4, true);
        Assert.Equal(CardRarity.UltraRara, service.RarityOf(4));
    }

    [Fact]
    public async Task Conta_por_tipo()
    {
        var service = await CreateAsync();
        await service.SetAsync(1, CardRarity.Normal);
        await service.SetAsync(2, CardRarity.Normal);
        await service.SetAsync(3, CardRarity.Foil);
        await service.SetAsync(6, CardRarity.ArteRara);

        var counts = service.CountByRarity();

        Assert.Equal(2, counts[CardRarity.Normal]);
        Assert.Equal(1, counts[CardRarity.Foil]);
        Assert.Equal(0, counts[CardRarity.UltraRara]);
        Assert.Equal(1, counts[CardRarity.ArteRara]);
    }

    [Fact]
    public async Task Tipo_persiste_no_banco()
    {
        var first = await CreateAsync();
        await first.SetAsync(150, CardRarity.ArteRara);

        var second = await CreateAsync();

        Assert.Equal(CardRarity.ArteRara, second.RarityOf(150));
    }

    [Fact]
    public async Task Banco_antigo_sem_coluna_de_tipo_vira_normal()
    {
        await using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE "collected" ("PokemonId" INTEGER NOT NULL CONSTRAINT "PK_collected" PRIMARY KEY, "CollectedAt" TEXT NOT NULL);
                INSERT INTO collected (PokemonId, CollectedAt) VALUES (152, '2026-11-14T10:00:00-03:00');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var service = await CreateAsync();

        Assert.Equal(CardRarity.Normal, service.RarityOf(152));
        await service.SetAsync(152, CardRarity.Foil);
        Assert.Equal(CardRarity.Foil, (await CreateAsync()).RarityOf(152));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(f)) File.Delete(f);
        }
    }

    private sealed class Factory(DbContextOptions<BinderDbContext> options) : IDbContextFactory<BinderDbContext>
    {
        public BinderDbContext CreateDbContext() => new(options);
    }
}
