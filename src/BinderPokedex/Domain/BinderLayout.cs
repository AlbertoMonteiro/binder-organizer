namespace BinderPokedex.Domain;

public enum BinderSide
{
    Frente = 0,
    Verso = 1,
}

/// <summary>Posição física de um bolso no binder.</summary>
/// <param name="Sheet">Folha (1..32).</param>
/// <param name="Side">Frente ou verso da folha.</param>
/// <param name="Row">Linha (1..4).</param>
/// <param name="Column">Coluna (1..4).</param>
public sealed record BinderSlot(int Sheet, BinderSide Side, int Row, int Column)
{
    /// <summary>Página corrida (1..64): folha 1 frente = 1, folha 1 verso = 2...</summary>
    public int Page => BinderLayout.PageOf(Sheet, Side);

    public string SideLabel => Side == BinderSide.Frente ? "Frente" : "Verso";

    public string Description => $"Folha {Sheet} · {SideLabel} · Linha {Row} · Coluna {Column}";
}

/// <summary>Binder 4x4 com 32 folhas (16 bolsos por lado) = 1024 posições, em ordem da Pokédex Nacional.</summary>
public static class BinderLayout
{
    public const int Rows = 4;
    public const int Columns = 4;
    public const int PocketsPerPage = Rows * Columns;      // 16
    public const int PocketsPerSheet = PocketsPerPage * 2; // 32
    public const int Sheets = 32;
    public const int Pages = Sheets * 2;                    // 64
    public const int Capacity = Sheets * PocketsPerSheet;   // 1024

    public static bool Fits(int pokemonId) => pokemonId is >= 1 and <= Capacity;

    /// <summary>Retorna o bolso do Pokémon, ou null se ele não cabe no binder (#1025).</summary>
    public static BinderSlot? SlotOf(int pokemonId)
    {
        if (!Fits(pokemonId)) return null;

        var index = pokemonId - 1;
        var sheet = index / PocketsPerSheet + 1;
        var side = (BinderSide)(index % PocketsPerSheet / PocketsPerPage);
        var pocket = index % PocketsPerPage;
        return new BinderSlot(sheet, side, pocket / Columns + 1, pocket % Columns + 1);
    }

    public static int PageOf(int sheet, BinderSide side) => (sheet - 1) * 2 + (int)side + 1;

    public static int PageOf(int pokemonId) => SlotOf(pokemonId)?.Page
        ?? throw new ArgumentOutOfRangeException(nameof(pokemonId), pokemonId, "Pokémon não cabe no binder.");

    public static (int Sheet, BinderSide Side) SheetOfPage(int page) =>
        ((page - 1) / 2 + 1, (BinderSide)((page - 1) % 2));

    /// <summary>Números da Pokédex dos 16 bolsos de uma página, em ordem de leitura.</summary>
    public static IEnumerable<int> PokemonIdsOnPage(int page)
    {
        var first = (page - 1) * PocketsPerPage + 1;
        return Enumerable.Range(first, PocketsPerPage);
    }

    /// <summary>
    /// Binder aberto: à esquerda fica o verso de uma folha e à direita a frente da próxima.
    /// Abertura 0 = só a frente da folha 1; abertura 32 = só o verso da folha 32.
    /// </summary>
    public const int Spreads = Sheets + 1; // 0..32

    public static (int? LeftPage, int? RightPage) PagesOfSpread(int spread)
    {
        int? left = spread == 0 ? null : spread * 2;           // verso da folha 'spread'
        int? right = spread == Sheets ? null : spread * 2 + 1; // frente da folha 'spread + 1'
        return (left, right);
    }

    public static int SpreadOfPage(int page) => page / 2;

    /// <summary>Quanto virar para ir do binder aberto em <paramref name="fromPage"/> até <paramref name="toPage"/>.</summary>
    public static BinderTurn Turn(int fromPage, int toPage) =>
        new(SpreadOfPage(toPage) - SpreadOfPage(fromPage), toPage - fromPage);
}

/// <summary>Folhas a virar e páginas de diferença; positivo = avançar, negativo = voltar.</summary>
public sealed record BinderTurn(int Sheets, int Pages)
{
    public bool SameSpread => Sheets == 0;
    public bool Forward => Sheets > 0;

    /// <summary>Resumo curto, ex.: "↪ 3 folhas", "↩ 12 folhas", "aberto aqui".</summary>
    public string Short => Sheets switch
    {
        0 => "aberto aqui",
        > 0 => $"↪ {Sheets} {(Sheets == 1 ? "folha" : "folhas")}",
        _ => $"↩ {-Sheets} {(Sheets == -1 ? "folha" : "folhas")}",
    };
}
