using BinderPokedex.Domain;
using BinderPokedex.Services;

namespace BinderPokedex.Tests;

public class GenerationsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(151, 1)]
    [InlineData(152, 2)]
    [InlineData(251, 2)]
    [InlineData(252, 3)]
    [InlineData(386, 3)]
    [InlineData(387, 4)]
    [InlineData(493, 4)]
    [InlineData(494, 5)]
    [InlineData(649, 5)]
    [InlineData(650, 6)]
    [InlineData(721, 6)]
    [InlineData(722, 7)]
    [InlineData(809, 7)]
    [InlineData(810, 8)]
    [InlineData(905, 8)]
    [InlineData(906, 9)]
    [InlineData(1025, 9)]
    public void ForPokemon_respects_boundaries(int id, int generation) =>
        Assert.Equal(generation, Generations.ForPokemon(id).Number);

    [Fact]
    public void Generation_ranges_cover_the_whole_pokedex_without_gaps()
    {
        var expected = 1;
        foreach (var g in Generations.All)
        {
            Assert.Equal(expected, g.FirstId);
            expected = g.LastId + 1;
        }
        Assert.Equal(1026, expected);
    }

    [Fact]
    public void Embedded_data_agrees_with_generation_ranges()
    {
        var pokedex = new PokedexService();

        Assert.Equal(1025, pokedex.All.Count);
        Assert.All(pokedex.All, p => Assert.Equal(Generations.ForPokemon(p.Id).Number, p.Generation));
    }
}
