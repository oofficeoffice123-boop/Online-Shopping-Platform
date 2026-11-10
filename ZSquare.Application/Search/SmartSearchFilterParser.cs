using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZSquare.Application.Search;

public sealed class SmartSearchFilterParser
{
    private const decimal MaximumPrice = 1_000_000m;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool TryParse(string json, SearchCatalogValues allowed, out ProductFilterDto filters)
    {
        filters = new ProductFilterDto();
        RawFilters? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawFilters>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (raw is null)
            return false;

        var minPrice = ValidPrice(raw.MinPrice);
        var maxPrice = ValidPrice(raw.MaxPrice);
        if (minPrice is not null && maxPrice is not null && minPrice > maxPrice)
            (minPrice, maxPrice) = (null, null);

        filters = new ProductFilterDto
        {
            Category = MapCategory(raw.Category, allowed.Categories),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Color = MatchColor(raw.Color, allowed.Colors),
            Size = MatchAllowed(raw.Size, allowed.Sizes),
            Keyword = ValidKeyword(raw.Keyword),
            Sort = ValidSort(raw.Sort)
        };
        return true;
    }

    private static decimal? ValidPrice(decimal? value) =>
        value is >= 0 and <= MaximumPrice ? value : null;

    private static string? ValidKeyword(string? value)
    {
        var keyword = value?.Trim();
        return !string.IsNullOrEmpty(keyword) && keyword.Length <= 100 && !keyword.Any(char.IsControl)
            ? keyword
            : null;
    }

    private static string? ValidSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "relevance" => "relevance",
        "priceasc" => "priceAsc",
        "pricedesc" => "priceDesc",
        "newest" => "newest",
        _ => null
    };

    private static string? MapCategory(string? value, IReadOnlyList<string> allowed)
    {
        var normalized = Normalize(value);
        var alias = normalized switch
        {
            "unstitched" or "fabric" or "fabrics" => "unstitched fabric",
            "kurta" or "kurtas" or "kameez" => "kurtas and kameez",
            "shirt" or "shirts" or "top" or "tops" => "shirts and tops",
            "dress" => "dresses",
            "bottom" => "bottoms",
            "dupatta" or "stole" or "shawl" => "dupattas and shawls",
            _ => normalized
        };
        return allowed.FirstOrDefault(candidate => Normalize(candidate) == alias);
    }

    private static string? MatchAllowed(string? value, IReadOnlyList<string> allowed)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0 ? null : allowed.FirstOrDefault(candidate => Normalize(candidate) == normalized);
    }

    private static string? MatchColor(string? value, IReadOnlyList<string> allowed)
    {
        var normalized = KhaadiCatalogMapping.NormalizeColor(value);
        return normalized.Length == 0
            ? null
            : allowed.FirstOrDefault(candidate => KhaadiCatalogMapping.NormalizeColor(candidate) == normalized);
    }

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private sealed class RawFilters
    {
        public string? Category { get; init; }
        public decimal? MinPrice { get; init; }
        public decimal? MaxPrice { get; init; }
        public string? Color { get; init; }
        public string? Keyword { get; init; }
        public string? Size { get; init; }
        public string? Sort { get; init; }
    }
}
