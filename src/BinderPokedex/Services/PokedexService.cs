using System.Globalization;
using System.Text;
using System.Text.Json;
using BinderPokedex.Domain;

namespace BinderPokedex.Services;

public sealed class PokedexService
{
    private readonly Pokemon[] _all;
    private readonly string[] _normalizedNames;

    public PokedexService() : this(LoadEmbedded()) { }

    public PokedexService(IEnumerable<Pokemon> pokemon)
    {
        _all = pokemon.OrderBy(p => p.Id).ToArray();
        _normalizedNames = _all.Select(p => Normalize(p.Name)).ToArray();
    }

    public IReadOnlyList<Pokemon> All => _all;

    /// <summary>Pokémon que cabem no binder (#1..#1024).</summary>
    public IEnumerable<Pokemon> InBinder => _all.Where(p => BinderLayout.Fits(p.Id));

    public Pokemon? Get(int id) => id >= 1 && id <= _all.Length && _all[id - 1].Id == id
        ? _all[id - 1]
        : _all.FirstOrDefault(p => p.Id == id);

    /// <summary>Busca por número (ex.: "25", "#025") ou por nome, ignorando acentos, caixa e pontuação.</summary>
    public IReadOnlyList<Pokemon> Search(string? query, int max = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var trimmed = query.Trim().TrimStart('#');
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            var exact = Get(number);
            var byNumber = _all.Where(p => p.Id != number && p.Id.ToString(CultureInfo.InvariantCulture).StartsWith(trimmed.TrimStart('0'), StringComparison.Ordinal));
            return (exact is null ? byNumber : byNumber.Prepend(exact)).Take(max).ToList();
        }

        var needle = Normalize(query);
        if (needle.Length == 0) return [];

        return _all
            .Select((p, i) => (Pokemon: p, Name: _normalizedNames[i]))
            .Where(x => x.Name.Contains(needle, StringComparison.Ordinal))
            .OrderBy(x => x.Name == needle ? 0 : x.Name.StartsWith(needle, StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(x => x.Pokemon.Id)
            .Select(x => x.Pokemon)
            .Take(max)
            .ToList();
    }

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c == '♀') sb.Append('f');
            else if (c == '♂') sb.Append('m');
        }
        return sb.ToString();
    }

    private static IEnumerable<Pokemon> LoadEmbedded()
    {
        var assembly = typeof(PokedexService).Assembly;
        using var stream = assembly.GetManifestResourceStream("BinderPokedex.Data.pokemon.json")
            ?? throw new InvalidOperationException("Recurso pokemon.json não encontrado.");
        return JsonSerializer.Deserialize<Pokemon[]>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("pokemon.json vazio.");
    }
}
