namespace BinderPokedex.Domain;

public sealed record Pokemon(int Id, string Name, int Generation, string[] Types)
{
    public string Number => $"#{Id:0000}";

    public string SpriteUrl =>
        $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/{Id}.png";

    public GenerationInfo GenerationInfo => Generations.Get(Generation);
}
