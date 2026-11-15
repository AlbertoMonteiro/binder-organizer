using BinderPokedex.Services;

namespace BinderPokedex.Tests;

public class PokedexServiceTests
{
    private static readonly PokedexService Pokedex = new();

    [Fact]
    public void Search_by_partial_name_is_case_insensitive()
    {
        var results = Pokedex.Search("PIKA");
        Assert.Equal("Pikachu", results[0].Name);
    }

    [Theory]
    [InlineData("25")]
    [InlineData("025")]
    [InlineData("#025")]
    public void Search_by_number_returns_exact_match_first(string query) =>
        Assert.Equal(25, Pokedex.Search(query)[0].Id);

    [Theory]
    [InlineData("flabebe", "Flabébé")]
    [InlineData("mr mime", "Mr. Mime")]
    [InlineData("farfetchd", "Farfetch’d")]
    [InlineData("nidoranf", "Nidoran♀")]
    public void Search_ignores_accents_and_punctuation(string query, string expected) =>
        Assert.Contains(Pokedex.Search(query), p => p.Name == expected);

    [Fact]
    public void Search_prefers_exact_then_prefix_matches()
    {
        var results = Pokedex.Search("mew");
        Assert.Equal("Mew", results[0].Name);
        Assert.Equal("Mewtwo", results[1].Name);
    }

    [Fact]
    public void Search_with_blank_query_returns_nothing() => Assert.Empty(Pokedex.Search("  "));

    [Fact]
    public void Pecharunt_is_the_one_left_out()
    {
        Assert.Equal(1024, Pokedex.InBinder.Count());
        Assert.Equal("Pecharunt", Pokedex.Get(1025)!.Name);
    }
}
