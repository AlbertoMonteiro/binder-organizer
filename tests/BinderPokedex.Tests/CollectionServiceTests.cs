using BinderPokedex.Domain;
using BinderPokedex.Services;

namespace BinderPokedex.Tests;

public class CollectionServiceTests
{
    private sealed class FakeRemote : ICollectionRemote
    {
        public CollectionFile Stored = CollectionFile.Empty;
        public bool Configured = true;
        public Exception? FailWith;
        /// <summary>Simula o GitHub devolvendo uma versão antiga logo depois de gravar.</summary>
        public CollectionFile? StaleRead;
        public int Saves;

        public bool IsConfigured => Configured;

        public Task<CollectionFile> LoadAsync(CancellationToken ct = default)
        {
            if (FailWith is not null) throw FailWith;
            var result = StaleRead ?? Stored;
            StaleRead = null;
            return Task.FromResult(result);
        }

        public Task SaveAsync(CollectionFile file, CancellationToken ct = default)
        {
            if (FailWith is not null) throw FailWith;
            Saves++;
            Stored = file;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryCache : ICollectionCache
    {
        public (CollectionFile File, DateTimeOffset SyncedAt)? Saved;
        public (CollectionFile File, DateTimeOffset SyncedAt)? Load() => Saved;
        public void Save(CollectionFile file, DateTimeOffset syncedAt) => Saved = (file, syncedAt);
    }

    private static readonly PokedexService Pokedex = new();

    private static async Task<CollectionService> StartAsync(FakeRemote remote, MemoryCache? cache = null)
    {
        var service = new CollectionService(Pokedex, remote, cache ?? new MemoryCache());
        await service.InitializeAsync();
        await service.RefreshAsync();
        return service;
    }

    private static CollectionFile File(int revision, params (int Id, CardRarity Rarity)[] cards) =>
        new(revision, DateTimeOffset.UnixEpoch, cards.ToDictionary(c => c.Id, c => c.Rarity));

    [Fact]
    public async Task Set_change_and_remove_are_saved_to_the_remote()
    {
        var remote = new FakeRemote();
        var service = await StartAsync(remote);

        await service.SetAsync(6, CardRarity.Foil);
        await service.SetAsync(6, CardRarity.ArteRara);
        await service.SetAsync(25, true);

        Assert.Equal(CardRarity.ArteRara, service.RarityOf(6));
        Assert.Equal(CardRarity.Normal, remote.Stored.Cards[25]);
        Assert.Equal(3, remote.Stored.Revision);

        await service.SetAsync(6, (CardRarity?)null);
        Assert.False(remote.Stored.Cards.ContainsKey(6));
        Assert.Equal(1, service.CollectedCount);
    }

    [Fact]
    public async Task Marking_as_collected_keeps_existing_type()
    {
        var remote = new FakeRemote { Stored = File(1, (6, CardRarity.UltraRara)) };
        var service = await StartAsync(remote);

        await service.SetAsync(6, true);

        Assert.Equal(CardRarity.UltraRara, service.RarityOf(6));
        Assert.Equal(0, remote.Saves);
    }

    [Fact]
    public async Task Change_from_another_device_is_kept_when_saving()
    {
        var remote = new FakeRemote();
        var service = await StartAsync(remote);

        // Outro aparelho marca o #1 depois que este leu o gist.
        remote.Stored = File(5, (1, CardRarity.Normal));
        await service.SetAsync(4, CardRarity.Foil);

        Assert.True(remote.Stored.Cards.ContainsKey(1));
        Assert.True(remote.Stored.Cards.ContainsKey(4));
        Assert.Equal(6, remote.Stored.Revision);
        Assert.True(service.Has(1));
    }

    [Fact]
    public async Task Stale_read_after_own_save_does_not_lose_the_previous_change()
    {
        var remote = new FakeRemote();
        var service = await StartAsync(remote);
        await service.SetAsync(1, CardRarity.Normal);

        remote.StaleRead = CollectionFile.Empty; // GitHub ainda devolve a versão de antes
        await service.SetAsync(2, CardRarity.Normal);

        Assert.True(remote.Stored.Cards.ContainsKey(1));
        Assert.True(remote.Stored.Cards.ContainsKey(2));
    }

    [Fact]
    public async Task Offline_shows_cached_copy_and_is_read_only()
    {
        var cache = new MemoryCache();
        cache.Save(File(3, (150, CardRarity.ArteRara)), DateTimeOffset.UnixEpoch.AddDays(1));
        var remote = new FakeRemote { FailWith = new HttpRequestException("sem internet") };

        var service = await StartAsync(remote, cache);

        Assert.Equal(SyncStatus.Offline, service.Status);
        Assert.False(service.CanEdit);
        Assert.Equal(CardRarity.ArteRara, service.RarityOf(150));

        await service.SetAsync(151, CardRarity.Normal);
        Assert.False(service.Has(151));
    }

    [Fact]
    public async Task Failed_save_reverts_and_goes_read_only()
    {
        var remote = new FakeRemote();
        var service = await StartAsync(remote);

        remote.FailWith = new HttpRequestException("caiu");
        await service.SetAsync(7, CardRarity.Normal);

        Assert.False(service.Has(7));
        Assert.Equal(SyncStatus.Offline, service.Status);
        Assert.Equal("caiu", service.LastError);
    }

    [Fact]
    public async Task Invalid_token_is_reported_as_auth_error()
    {
        var remote = new FakeRemote { FailWith = new RemoteAuthException("token inválido") };
        var service = await StartAsync(remote);

        Assert.Equal(SyncStatus.AuthError, service.Status);
        Assert.False(service.CanEdit);
    }

    [Fact]
    public async Task Without_token_is_not_configured()
    {
        var service = await StartAsync(new FakeRemote { Configured = false });

        Assert.Equal(SyncStatus.NotConfigured, service.Status);
        Assert.False(service.CanEdit);
    }

    [Fact]
    public async Task Successful_sync_updates_the_device_cache()
    {
        var cache = new MemoryCache();
        var remote = new FakeRemote { Stored = File(2, (10, CardRarity.Foil)) };

        await StartAsync(remote, cache);

        Assert.NotNull(cache.Saved);
        Assert.Equal(CardRarity.Foil, cache.Saved!.Value.File.Cards[10]);
    }

    [Fact]
    public async Task Counts_by_rarity()
    {
        var remote = new FakeRemote { Stored = File(1, (1, CardRarity.Normal), (2, CardRarity.Normal), (3, CardRarity.Foil), (4, CardRarity.ArteRara)) };
        var service = await StartAsync(remote);

        var counts = service.CountByRarity();

        Assert.Equal(2, counts[CardRarity.Normal]);
        Assert.Equal(1, counts[CardRarity.Foil]);
        Assert.Equal(0, counts[CardRarity.UltraRara]);
        Assert.Equal(1, counts[CardRarity.ArteRara]);
    }

    [Fact]
    public void Collection_file_round_trips_as_readable_json()
    {
        var file = File(7, (25, CardRarity.Normal), (6, CardRarity.ArteRara));

        var json = file.ToJson();
        var back = CollectionFile.FromJson(json);

        Assert.Contains("\"ArteRara\"", json);
        Assert.Equal(7, back.Revision);
        Assert.Equal(CardRarity.ArteRara, back.Cards[6]);
        Assert.Equal(2, back.Cards.Count);
    }

    [Fact]
    public void Collection_file_ignores_ids_outside_the_binder()
    {
        var back = CollectionFile.FromJson("""{ "revision": 1, "cards": { "1": "Normal", "1025": "Foil" } }""");

        Assert.Single(back.Cards);
    }
}
