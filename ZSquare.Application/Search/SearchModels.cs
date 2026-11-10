namespace ZSquare.Application.Search;

public sealed class SmartSearchRequestDto
{
    public string? Query { get; set; }
}

public sealed record ProductFilterDto
{
    public int? CategoryId { get; init; }
    public string? Category { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? Color { get; init; }
    public IReadOnlyList<string>? Colors { get; init; }
    public string? Keyword { get; init; }
    public string? Size { get; init; }
    public IReadOnlyList<string>? Sizes { get; init; }
    public string? Sort { get; init; }
    public bool InStockOnly { get; init; }
    public decimal? MinRating { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ProductCardDto(
    int ProductId,
    string ProductCode,
    string Name,
    string? Description,
    string Category,
    decimal Price,
    string? PrimaryImageUrl)
{
    public int Id => ProductId;
    public string CategoryName => Category;
    public string? SecondaryImageUrl { get; init; }
    public IReadOnlyList<string> AvailableColors { get; init; } = [];
    public bool InStock { get; init; }
    public bool LowStock { get; init; }
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
}

public sealed record ProductImageDto(int Id, string Url, bool IsPrimary, int SortOrder);

public sealed record ProductVariantDto(
    int VariantId,
    string Size,
    string Color,
    decimal Price,
    bool InStock,
    bool LowStock);

public sealed record ProductDetailsDto(
    int Id,
    string ProductCode,
    string Name,
    string? Description,
    string CategoryName,
    decimal Price,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductVariantDto> Variants)
{
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
}

public sealed record CategoryListItemDto(int CategoryId, int? ParentCategoryId, string Name, int ProductCount);

public sealed record ProductFilterOptionsDto(
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Sizes,
    decimal? MinPrice,
    decimal? MaxPrice);

public sealed record PagedProductListDto(
    IReadOnlyList<ProductCardDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record SmartSearchResponseDto(
    PagedProductListDto Products,
    ProductFilterDto InterpretedFilters,
    bool Fallback);

public sealed record SearchCatalogValues(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Sizes);

public interface ISmartSearchService
{
    Task<SmartSearchResponseDto> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public interface IProductFilterInterpreter
{
    Task<string> InterpretAsync(string systemPrompt, string userQuery, CancellationToken cancellationToken = default);
}

public interface IProductSearchService
{
    Task<PagedProductListDto> SearchAsync(ProductFilterDto filters, CancellationToken cancellationToken = default);
}

public interface IProductBrowsingService
{
    Task<ProductDetailsDto?> GetProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}

public interface ISearchCatalogProvider
{
    Task<SearchCatalogValues> GetValuesAsync(CancellationToken cancellationToken = default);
}
