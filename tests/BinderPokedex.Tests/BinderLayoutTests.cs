using BinderPokedex.Domain;

namespace BinderPokedex.Tests;

public class BinderLayoutTests
{
    [Theory]
    [InlineData(64, 41, -12, -23)] // Genesect: da última página volta 12 folhas
    [InlineData(41, 64, 12, 23)]
    [InlineData(1, 2, 1, 1)]       // frente da folha 1 -> verso: vira 1 folha
    [InlineData(2, 3, 0, 1)]       // verso da folha 1 e frente da folha 2 ficam abertas juntas
    [InlineData(10, 10, 0, 0)]
    public void Turn_conta_folhas_a_virar(int from, int to, int sheets, int pages)
    {
        var turn = BinderLayout.Turn(from, to);
        Assert.Equal(sheets, turn.Sheets);
        Assert.Equal(pages, turn.Pages);
    }

    [Theory]
    [InlineData(1, 1, BinderSide.Frente, 1, 1, 1)]
    [InlineData(4, 1, BinderSide.Frente, 1, 4, 1)]
    [InlineData(5, 1, BinderSide.Frente, 2, 1, 1)]
    [InlineData(16, 1, BinderSide.Frente, 4, 4, 1)]
    [InlineData(17, 1, BinderSide.Verso, 1, 1, 2)]
    [InlineData(32, 1, BinderSide.Verso, 4, 4, 2)]
    [InlineData(33, 2, BinderSide.Frente, 1, 1, 3)]
    [InlineData(152, 5, BinderSide.Verso, 2, 4, 10)]
    [InlineData(1024, 32, BinderSide.Verso, 4, 4, 64)]
    public void SlotOf_returns_physical_position(int id, int sheet, BinderSide side, int row, int col, int page)
    {
        var slot = BinderLayout.SlotOf(id);

        Assert.NotNull(slot);
        Assert.Equal(new BinderSlot(sheet, side, row, col), slot);
        Assert.Equal(page, slot.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1025)]
    public void SlotOf_returns_null_when_pokemon_does_not_fit(int id) =>
        Assert.Null(BinderLayout.SlotOf(id));

    [Fact]
    public void Every_pocket_is_used_exactly_once()
    {
        var slots = Enumerable.Range(1, BinderLayout.Capacity).Select(id => BinderLayout.SlotOf(id)!).ToList();

        Assert.Equal(BinderLayout.Capacity, slots.Distinct().Count());
        Assert.All(slots, s => Assert.InRange(s.Sheet, 1, 32));
    }

    [Fact]
    public void PokemonIdsOnPage_matches_SlotOf()
    {
        for (var page = 1; page <= BinderLayout.Pages; page++)
        {
            var ids = BinderLayout.PokemonIdsOnPage(page).ToList();
            Assert.Equal(16, ids.Count);
            Assert.All(ids, id => Assert.Equal(page, BinderLayout.SlotOf(id)!.Page));
        }
    }

    [Theory]
    [InlineData(0, null, 1)]
    [InlineData(1, 2, 3)]
    [InlineData(16, 32, 33)]
    [InlineData(32, 64, null)]
    public void PagesOfSpread_shows_back_of_sheet_left_and_front_of_next_right(int spread, int? left, int? right)
    {
        Assert.Equal((left, right), BinderLayout.PagesOfSpread(spread));
        if (left is { } l) Assert.Equal(spread, BinderLayout.SpreadOfPage(l));
        if (right is { } r) Assert.Equal(spread, BinderLayout.SpreadOfPage(r));
    }
}
