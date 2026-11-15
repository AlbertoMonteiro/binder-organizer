using BinderPokedex.Services;
using Microsoft.AspNetCore.Components;

namespace BinderPokedex.Components.Shared;

/// <summary>
/// Re-renderiza quando a coleção muda (inclusive por outro aparelho conectado)
/// ou quando muda a página em que o binder está aberto neste aparelho.
/// </summary>
public abstract class CollectionAwareComponent : ComponentBase, IDisposable
{
    [Inject] protected CollectionService Collection { get; set; } = null!;
    [Inject] protected PokedexService Pokedex { get; set; } = null!;
    [Inject] protected ReadingPosition Position { get; set; } = null!;

    protected override void OnInitialized()
    {
        Collection.Changed += OnStateChanged;
        Position.Changed += OnStateChanged;
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Collection.Changed -= OnStateChanged;
        Position.Changed -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}
