using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZSquare.Application.Search;

namespace ZSquare.API.Controllers;

[ApiController]
[Route("api/products")]
[AllowAnonymous]
public sealed class ProductsController(
    IProductSearchService search,
    IProductBrowsingService browsing) : ControllerBase
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "newest", "priceAsc", "priceDesc", "nameAsc", "topRated"
    };

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductListQuery request, CancellationToken cancellationToken)
    {
        var categories = await browsing.GetCategoriesAsync(cancellationToken);
        var filterOptions = await browsing.GetFilterOptionsAsync(cancellationToken);
        var errors = new Dictionary<string, string[]>();

        if (request.CategoryId is not null && categories.All(category => category.CategoryId != request.CategoryId))
            errors[nameof(request.CategoryId)] = ["Choose a category returned by /api/categories."];
        if (request.MinPrice is < 0 or > 1_000_000)
            errors[nameof(request.MinPrice)] = ["Minimum price must be between 0 and 1000000 PKR."];
        if (request.MaxPrice is < 0 or > 1_000_000)
            errors[nameof(request.MaxPrice)] = ["Maximum price must be between 0 and 1000000 PKR."];
        if (request.MinRating is < 1 or > 5)
            errors[nameof(request.MinRating)] = ["Minimum rating must be between 1 and 5."];
        if (request.MinPrice is not null && request.MaxPrice is not null && request.MinPrice > request.MaxPrice)
            errors["price"] = ["Minimum price cannot exceed maximum price."];
        if (request.Q?.Trim().Length > 200)
            errors[nameof(request.Q)] = ["Search text cannot exceed 200 characters."];
        if (!string.IsNullOrWhiteSpace(request.Sort) && !AllowedSorts.Contains(request.Sort.Trim()))
            errors[nameof(request.Sort)] = ["Sort must be newest, priceAsc, priceDesc, nameAsc, or topRated."];

        var colors = ResolveValues(request.Color, filterOptions.Colors, nameof(request.Color), errors);
        var sizes = ResolveValues(request.Size, filterOptions.Sizes, nameof(request.Size), errors);
        if (errors.Count > 0)
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more product filters are invalid."
            });

        var page = Math.Max(1, request.Page ?? 1);
        var pageSize = Math.Clamp(request.PageSize ?? 12, 1, 48);
        var filters = new ProductFilterDto
        {
            CategoryId = request.CategoryId,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            Colors = colors,
            Sizes = sizes,
            Keyword = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            InStockOnly = request.InStockOnly,
            MinRating = request.MinRating,
            Sort = string.IsNullOrWhiteSpace(request.Sort) ? "newest" : request.Sort.Trim(),
            Page = page,
            PageSize = pageSize
        };
        return Ok(await search.SearchAsync(filters, cancellationToken));
    }

    [HttpGet("filters")]
    public async Task<ActionResult<ProductFilterOptionsDto>> Filters(CancellationToken cancellationToken) =>
        Ok(await browsing.GetFilterOptionsAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var product = await browsing.GetProductAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    private static string[] ResolveValues(
        IReadOnlyCollection<string>? requested,
        IReadOnlyList<string> allowed,
        string propertyName,
        IDictionary<string, string[]> errors)
    {
        if (requested is null || requested.Count == 0)
            return [];

        var result = new List<string>();
        foreach (var value in requested.Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            var canonical = allowed.FirstOrDefault(candidate => candidate.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                errors[propertyName] = [$"'{value}' is not an available {propertyName.ToLowerInvariant()} value."];
            else if (!result.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                result.Add(canonical);
        }

        return result.ToArray();
    }
}

public sealed class ProductListQuery
{
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string[]? Color { get; set; }
    public string[]? Size { get; set; }
    public string? Q { get; set; }
    public bool InStockOnly { get; set; }
    public decimal? MinRating { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
