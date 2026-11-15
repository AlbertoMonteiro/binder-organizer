namespace BinderPokedex.Domain;

public sealed record GenerationInfo(int Number, string Region, int FirstId, int LastId, string Color)
{
    public string Label => $"{Number}ª geração";
    public string FullLabel => $"{Number}ª geração — {Region}";
    public int Count => LastId - FirstId + 1;
}

public static class Generations
{
    public static IReadOnlyList<GenerationInfo> All { get; } =
    [
        new(1, "Kanto", 1, 151, "#e5534b"),
        new(2, "Johto", 152, 251, "#d4a72c"),
        new(3, "Hoenn", 252, 386, "#3fa45b"),
        new(4, "Sinnoh", 387, 493, "#4c8eda"),
        new(5, "Unova", 494, 649, "#6e7781"),
        new(6, "Kalos", 650, 721, "#d15fa6"),
        new(7, "Alola", 722, 809, "#e8833a"),
        new(8, "Galar", 810, 905, "#8e6bd8"),
        new(9, "Paldea", 906, 1025, "#c0392b"),
    ];

    public static GenerationInfo Get(int number) =>
        number is >= 1 and <= 9
            ? All[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number), number, "Geração deve estar entre 1 e 9.");

    public static GenerationInfo ForPokemon(int id) =>
        All.FirstOrDefault(g => id >= g.FirstId && id <= g.LastId)
        ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Número da Pokédex inválido.");
}
