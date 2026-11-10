using System.Globalization;

namespace ZSquare.Application.Search;

public static class KhaadiCatalogMapping
{
    private static readonly (string Category, string[] Keywords)[] CategoryRules =
    [
        ("Unstitched Fabric", ["Fabrics"]),
        ("Dresses", ["Dress", "Jumpsuit"]),
        ("Kurtas and Kameez", ["Kameez", "Kurta", "Choga", "Kaftan"]),
        ("Shirts and Tops", ["Shirt", "T-shirt", "Blouse", "Tunic", "Peplum", "Polo", "Tank Top", "Camisole", "Sweatshirt", "Drop Shoulder", "Raglan"]),
        ("Bottoms", ["Shalwar", "Trousers", "Pants", "Culottes", "Jeans", "Skirt"]),
        ("Dupattas and Shawls", ["Dupatta", "Stole", "Shawl"])
    ];

    public static string? MapProductCategory(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return null;

        return CategoryRules.FirstOrDefault(rule => rule.Keywords.Any(keyword =>
            productName.Contains(keyword, StringComparison.OrdinalIgnoreCase))).Category;
    }

    public static IReadOnlyList<(string Category, IReadOnlyList<string> Keywords)> GetCategoryRules() =>
        CategoryRules.Select(rule => (rule.Category, (IReadOnlyList<string>)rule.Keywords)).ToArray();

    public static string NormalizeColor(string? value)
    {
        var color = string.Join(' ', (value ?? string.Empty)
            .Trim()
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        color = color.Replace("-", " ", StringComparison.Ordinal);

        if (color.StartsWith("D ", StringComparison.OrdinalIgnoreCase))
            color = "Dark " + color[2..];
        else if (color.StartsWith("L ", StringComparison.OrdinalIgnoreCase))
            color = "Light " + color[2..];

        var display = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(color.ToLowerInvariant());
        return display.Equals("Off White", StringComparison.OrdinalIgnoreCase) ? "Off-White" : display;
    }
}
