using BinderPokedex.Domain;

namespace BinderPokedex.Services;

public sealed record GenerationProgress(GenerationInfo Generation, int Collected, int Total)
{
    public double Percent => Total == 0 ? 0 : Collected * 100.0 / Total;
}

public enum SyncStatus
{
    /// <summary>Sem token neste aparelho: só mostra a cópia local (se houver).</summary>
    NotConfigured,
    /// <summary>Ainda não conseguiu falar com o GitHub desde que abriu.</summary>
    Connecting,
    Online,
    /// <summary>Sem internet ou GitHub fora: só leitura, com a última cópia guardada.</summary>
    Offline,
    /// <summary>Token inválido ou sem permissão de gist.</summary>
    AuthError,
}

/// <summary>
/// Quais bolsos já têm carta e de que tipo. A fonte da verdade é o gist; o aparelho guarda uma cópia
/// para abrir na hora e funcionar sem internet (só leitura). Cada alteração relê o gist antes de gravar,
/// para não apagar o que outro aparelho marcou nesse meio-tempo.
/// </summary>
public sealed class CollectionService(PokedexService pokedex, ICollectionRemote remote, ICollectionCache cache, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<(int Id, CardRarity? Rarity)> _pending = [];

    /// <summary>Última versão confirmada (lida ou gravada no gist).</summary>
    private CollectionFile _confirmed = CollectionFile.Empty;
    /// <summary>O que a tela mostra: confirmado + alterações ainda sendo gravadas.</summary>
    private IReadOnlyDictionary<int, CardRarity> _collected = new Dictionary<int, CardRarity>();

    public event Action? Changed;

    public SyncStatus Status { get; private set; } = SyncStatus.NotConfigured;
    public DateTimeOffset? LastSync { get; private set; }
    public string? LastError { get; private set; }
    public bool IsSaving => _pending.Count > 0;

    /// <summary>Só dá para alterar com o gist acessível; offline fica só leitura.</summary>
    public bool CanEdit => Status == SyncStatus.Online;

    /// <summary>Mostra na hora a cópia do aparelho e atualiza a partir do gist em segundo plano.</summary>
    public Task InitializeAsync()
    {
        if (cache.Load() is { } cached)
        {
            _confirmed = cached.File;
            LastSync = cached.SyncedAt == DateTimeOffset.MinValue ? null : cached.SyncedAt;
            Recompute();
        }
        Status = remote.IsConfigured ? SyncStatus.Connecting : SyncStatus.NotConfigured;
        _ = RefreshAsync();
        return Task.CompletedTask;
    }

    /// <summary>Relê o gist (ao abrir, ao voltar para o app, ao trocar o token ou no botão de tentar de novo).</summary>
    public async Task RefreshAsync()
    {
        if (!remote.IsConfigured)
        {
            Status = SyncStatus.NotConfigured;
            Changed?.Invoke();
            return;
        }

        await _lock.WaitAsync();
        try
        {
            Accept(await remote.LoadAsync());
        }
        catch (Exception e)
        {
            Fail(e);
        }
        finally
        {
            _lock.Release();
        }
        Recompute();
        Changed?.Invoke();
    }

    /// <summary>Relê só se a última sincronização tiver mais de <paramref name="maxAge"/>.</summary>
    public Task RefreshIfStaleAsync(TimeSpan maxAge) =>
        IsSaving || (Status == SyncStatus.Online && LastSync is { } last && _clock.GetUtcNow() - last < maxAge)
            ? Task.CompletedTask
            : RefreshAsync();

    /// <summary>Esquece a cópia confirmada (ao trocar de token/conta).</summary>
    public Task ResetAsync()
    {
        _confirmed = CollectionFile.Empty;
        LastSync = null;
        cache.Save(_confirmed, DateTimeOffset.MinValue);
        Recompute();
        return RefreshAsync();
    }

    public bool Has(int pokemonId) => _collected.ContainsKey(pokemonId);

    public CardRarity? RarityOf(int pokemonId) =>
        _collected.TryGetValue(pokemonId, out var rarity) ? rarity : null;

    public int CollectedCount => _collected.Count;

    public IReadOnlyDictionary<int, CardRarity> Snapshot() => _collected;

    /// <summary>Coloca a carta com o tipo informado (ou troca o tipo); null tira do binder.</summary>
    public async Task SetAsync(int pokemonId, CardRarity? rarity)
    {
        if (!BinderLayout.Fits(pokemonId)) throw new ArgumentOutOfRangeException(nameof(pokemonId));
        if (!CanEdit || RarityOf(pokemonId) == rarity) return;

        // Mostra na hora; se a gravação falhar, volta ao que estava.
        var change = (pokemonId, rarity);
        _pending.Add(change);
        Recompute();
        Changed?.Invoke();

        await _lock.WaitAsync();
        try
        {
            var latest = await remote.LoadAsync();
            // O GitHub às vezes devolve uma versão de segundos atrás: nunca volta para antes do que já foi gravado aqui.
            var current = latest.Revision >= _confirmed.Revision ? latest : _confirmed;
            var updated = current.With(pokemonId, rarity, _clock.GetUtcNow());
            await remote.SaveAsync(updated);
            Accept(updated);
        }
        catch (Exception e)
        {
            Fail(e);
        }
        finally
        {
            _pending.Remove(change);
            _lock.Release();
        }
        Recompute();
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
        var snapshot = _collected;
        return CardRarities.All.ToDictionary(r => r, r => snapshot.Values.Count(v => v == r));
    }

    public IReadOnlyList<GenerationProgress> ProgressByGeneration()
    {
        var snapshot = _collected;
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
        var snapshot = _collected;
        return pokedex.InBinder
            .Where(p => !snapshot.ContainsKey(p.Id))
            .Where(p => generation is null || p.Generation == generation);
    }

    private void Accept(CollectionFile file)
    {
        // Leitura atrasada do GitHub (mais antiga do que a que este aparelho acabou de gravar): mantém a nossa.
        if (file.Revision < _confirmed.Revision && LastSync is not null && Status == SyncStatus.Online) file = _confirmed;
        _confirmed = file;
        LastSync = _clock.GetUtcNow();
        LastError = null;
        Status = SyncStatus.Online;
        cache.Save(file, LastSync.Value);
    }

    private void Fail(Exception e)
    {
        LastError = e.Message;
        Status = e is RemoteAuthException ? SyncStatus.AuthError : SyncStatus.Offline;
    }

    private void Recompute()
    {
        if (_pending.Count == 0)
        {
            _collected = _confirmed.Cards;
            return;
        }
        var view = _confirmed.Cards.ToDictionary();
        foreach (var (id, rarity) in _pending)
        {
            if (rarity is { } r) view[id] = r; else view.Remove(id);
        }
        _collected = view;
    }
}
