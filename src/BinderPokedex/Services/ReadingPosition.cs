using BinderPokedex.Domain;
using Microsoft.JSInterop;

namespace BinderPokedex.Services;

/// <summary>
/// Página em que o binder físico está aberto, por aparelho (guardada no localStorage do navegador).
/// Usada para dizer quantas folhas avançar ou voltar até uma carta.
/// </summary>
public sealed class ReadingPosition(IJSRuntime js)
{
    private const string StorageKey = "binder.page";
    private bool _loaded;

    public int? Page { get; private set; }

    public event Action? Changed;

    /// <summary>Lê a posição salva; chamar após o primeiro render (precisa de JS).</summary>
    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        if (Page is not null) return; // já foi definida nesta sessão

        try
        {
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (int.TryParse(saved, out var page) && page is >= 1 and <= BinderLayout.Pages)
            {
                Page = page;
                Changed?.Invoke();
            }
        }
        catch (Exception e) when (IsJsUnavailable(e))
        {
        }
    }

    public async Task SetAsync(int page)
    {
        page = Math.Clamp(page, 1, BinderLayout.Pages);
        if (Page == page) return;
        Page = page;
        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, page.ToString());
        }
        catch (Exception e) when (IsJsUnavailable(e))
        {
        }
    }

    /// <summary>Quanto virar até <paramref name="toPage"/>, ou null se não sei onde o binder está aberto.</summary>
    public BinderTurn? TurnTo(int toPage) => Page is { } from ? BinderLayout.Turn(from, toPage) : null;

    private static bool IsJsUnavailable(Exception e) =>
        e is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException;
}
