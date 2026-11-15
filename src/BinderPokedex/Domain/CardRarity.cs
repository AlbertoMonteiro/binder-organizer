namespace BinderPokedex.Domain;

/// <summary>Tipo da carta colocada no bolso.</summary>
public enum CardRarity
{
    Normal = 0,
    Foil = 1,
    UltraRara = 2,
    ArteRara = 3,
}

public static class CardRarities
{
    public static IReadOnlyList<CardRarity> All { get; } = Enum.GetValues<CardRarity>();

    extension(CardRarity rarity)
    {
        public string Label => rarity switch
        {
            CardRarity.Foil => "Foil",
            CardRarity.UltraRara => "Ultra rara",
            CardRarity.ArteRara => "Arte rara",
            _ => "Normal",
        };

        /// <summary>Selo curto mostrado no bolso; vazio para carta normal.</summary>
        public string Short => rarity switch
        {
            CardRarity.Foil => "F",
            CardRarity.UltraRara => "UR",
            CardRarity.ArteRara => "★",
            _ => "",
        };

        public string? Hint => rarity == CardRarity.ArteRara ? "SAR / full art" : null;

        public string CssClass => rarity switch
        {
            CardRarity.Foil => "rarity-foil",
            CardRarity.UltraRara => "rarity-ultra",
            CardRarity.ArteRara => "rarity-art",
            _ => "rarity-normal",
        };
    }
}
