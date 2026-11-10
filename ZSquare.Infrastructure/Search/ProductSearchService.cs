using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ZSquare.Application.Search;
using ZSquare.Domain;

namespace ZSquare.Infrastructure.Search;

public sealed class ProductSearchService(AppDbContext db, IMemoryCache cache) : IProductSearchService, IProductBrowsingService
{
    private static readonly TimeSpan FilterCacheDuration = TimeSpan.FromMinutes(3);

    public async Task<PagedProductListDto> SearchAsync(ProductFilterDto filters, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filters.Page);
        // The legacy /api/search/products endpoint keeps its former 50 item ceiling.
        var pageSize = Math.Clamp(filters.PageSize, 1, 50);
        var colors = (filters.Colors ?? [])
            .Concat(string.IsNullOrWhiteSpace(filters.Color) ? [] : [filters.Color])
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sizes = (filters.Sizes ?? [])
            .Concat(string.IsNullOrWhiteSpace(filters.Size) ? [] : [filters.Size])
            .Where(size => !string.IsNullOrWhiteSpace(size))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var reviewStats = db.Reviews.AsNoTracking().Where(review => !review.IsHidden)
            .GroupBy(review => review.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                AverageRating = group.Average(review => (decimal)review.Rating),
                ReviewCount = group.Count()
            });

        IQueryable<Product> query = db.Products.AsNoTracking().Where(product => product.IsActive);

        if (filters.CategoryId is not null)
            query = query.Where(product => product.CategoryId == filters.CategoryId);
        if (!string.IsNullOrWhiteSpace(filters.Category))
            query = query.Where(product => product.Category.Name == filters.Category);
        if (!string.IsNullOrWhiteSpace(filters.Keyword))
            query = query.Where(product => product.Name.Contains(filters.Keyword) ||
                                           (product.Description != null && product.Description.Contains(filters.Keyword)));
        if (filters.MinRating.HasValue)
            query = query.Where(product => reviewStats.Where(stats => stats.ProductId == product.ProductId)
                .Select(stats => (decimal?)stats.AverageRating).FirstOrDefault() >= filters.MinRating.Value);

        var hasVariantFilters = colors.Length > 0 || sizes.Length > 0 || filters.MinPrice is not null ||
                                filters.MaxPrice is not null || filters.InStockOnly;
        if (hasVariantFilters)
        {
            var min = filters.MinPrice;
            var max = filters.MaxPrice;
            query = query.Where(product => product.Variants.Any(variant =>
                (colors.Length == 0 || colors.Contains(variant.Color.Name)) &&
                (sizes.Length == 0 || sizes.Contains(variant.Size.Label)) &&
                (!filters.InStockOnly || variant.StockQty > 0) &&
                (min == null || (variant.PriceOverride ?? product.BasePrice) >= min) &&
                (max == null || (variant.PriceOverride ?? product.BasePrice) <= max)) ||
                (!product.Variants.Any() && colors.Length == 0 && sizes.Length == 0 && !filters.InStockOnly &&
                 (min == null || product.BasePrice >= min) && (max == null || product.BasePrice <= max)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var minimumVariantPrices = db.ProductVariants.AsNoTracking()
            .Select(variant => new
            {
                variant.ProductId,
                Price = variant.PriceOverride ?? variant.Product.BasePrice
            })
            .GroupBy(variant => variant.ProductId)
            .Select(group => new { ProductId = group.Key, Price = group.Min(variant => variant.Price) });
        query = filters.Sort?.ToLowerInvariant() switch
        {
            "priceasc" => query.OrderBy(product => minimumVariantPrices
                    .Where(price => price.ProductId == product.ProductId)
                    .Select(price => (decimal?)price.Price).FirstOrDefault() ?? product.BasePrice)
                .ThenBy(product => product.ProductId),
            "pricedesc" => query.OrderByDescending(product => minimumVariantPrices
                    .Where(price => price.ProductId == product.ProductId)
                    .Select(price => (decimal?)price.Price).FirstOrDefault() ?? product.BasePrice)
                .ThenBy(product => product.ProductId),
            "newest" => query.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.ProductId),
            "toprated" => query.OrderByDescending(product => reviewStats.Where(stats => stats.ProductId == product.ProductId)
                    .Select(stats => (decimal?)stats.AverageRating).FirstOrDefault() ?? 0m)
                .ThenByDescending(product => reviewStats.Where(stats => stats.ProductId == product.ProductId)
                    .Select(stats => (int?)stats.ReviewCount).FirstOrDefault() ?? 0)
                .ThenBy(product => product.ProductId),
            "relevance" when !string.IsNullOrWhiteSpace(filters.Keyword) => query
                .OrderByDescending(product => product.Name.StartsWith(filters.Keyword))
                .ThenBy(product => product.Name),
            "nameasc" or null or "" => query.OrderBy(product => product.Name).ThenBy(product => product.ProductId),
            _ => query.OrderBy(product => product.Name).ThenBy(product => product.ProductId)
        };

        var productRows = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new ProductCoreRow(
                product.ProductId, product.ProductCode, product.Name, product.Description, product.Category.Name,
                product.BasePrice,
                reviewStats.Where(stats => stats.ProductId == product.ProductId).Select(stats => (decimal?)stats.AverageRating).FirstOrDefault(),
                reviewStats.Where(stats => stats.ProductId == product.ProductId).Select(stats => (int?)stats.ReviewCount).FirstOrDefault() ?? 0))
            .ToListAsync(cancellationToken);
        var productIds = productRows.Select(product => product.Id).ToArray();
        var imageRows = productIds.Length == 0
            ? []
            : await db.ProductImages.AsNoTracking().Where(image => productIds.Contains(image.ProductId))
                .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                .Select(image => new ProductImageRow(image.ProductId, image.Url, image.IsPrimary, image.SortOrder))
                .ToListAsync(cancellationToken);
        var variantRows = productIds.Length == 0
            ? []
            : await db.ProductVariants.AsNoTracking().Where(variant => productIds.Contains(variant.ProductId))
                .Select(variant => new ProductVariantRow(
                    variant.ProductId, variant.Color.Name, variant.Size.Label,
                    variant.PriceOverride ?? variant.Product.BasePrice, variant.StockQty))
                .ToListAsync(cancellationToken);
        var imagesByProduct = imageRows.GroupBy(image => image.ProductId).ToDictionary(group => group.Key, group => group.ToList());
        var variantsByProduct = variantRows.GroupBy(variant => variant.ProductId).ToDictionary(group => group.Key, group => group.ToList());
        var items = productRows.Select(product =>
        {
            imagesByProduct.TryGetValue(product.Id, out var productImages);
            variantsByProduct.TryGetValue(product.Id, out var productVariants);
            productImages ??= [];
            productVariants ??= [];
            var orderedImages = productImages.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).ToArray();
            return new ProductCardDto(
                product.Id, product.ProductCode, product.Name, product.Description, product.CategoryName,
                productVariants.Count > 0 ? productVariants.Min(variant => variant.Price) : product.BasePrice,
                orderedImages.FirstOrDefault()?.Url)
            {
                SecondaryImageUrl = orderedImages.Skip(1).FirstOrDefault()?.Url,
                AvailableColors = productVariants.Where(variant => variant.StockQty > 0).Select(variant => variant.Color)
                    .Distinct().OrderBy(color => color).ToArray(),
                InStock = productVariants.Any(variant => variant.StockQty > 0),
                LowStock = productVariants.Any(variant => variant.StockQty > 0 && variant.StockQty <= 5),
                AverageRating = product.AverageRating is null ? null : decimal.Round(product.AverageRating.Value, 1),
                ReviewCount = product.ReviewCount
            };
        }).ToArray();

        return new PagedProductListDto(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ProductDetailsDto?> GetProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var reviewStats = db.Reviews.AsNoTracking().Where(review => !review.IsHidden)
            .GroupBy(review => review.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                AverageRating = group.Average(review => (decimal)review.Rating),
                ReviewCount = group.Count()
            });
        var product = await db.Products.AsNoTracking()
            .Where(product => product.IsActive && product.ProductId == productId)
            .Select(product => new ProductCoreRow(
                product.ProductId, product.ProductCode, product.Name, product.Description, product.Category.Name,
                product.BasePrice,
                reviewStats.Where(stats => stats.ProductId == product.ProductId).Select(stats => (decimal?)stats.AverageRating).FirstOrDefault(),
                reviewStats.Where(stats => stats.ProductId == product.ProductId).Select(stats => (int?)stats.ReviewCount).FirstOrDefault() ?? 0))
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
            return null;

        var images = await db.ProductImages.AsNoTracking().Where(image => image.ProductId == productId)
            .OrderBy(image => image.SortOrder)
            .Select(image => new ProductImageDto(image.ImageId, image.Url, image.IsPrimary, image.SortOrder))
            .ToListAsync(cancellationToken);
        var variants = await db.ProductVariants.AsNoTracking().Where(variant => variant.ProductId == productId)
            .OrderBy(variant => variant.Size.Label).ThenBy(variant => variant.Color.Name)
            .Select(variant => new ProductVariantDto(
                variant.VariantId, variant.Size.Label, variant.Color.Name,
                variant.PriceOverride ?? variant.Product.BasePrice,
                variant.StockQty > 0, variant.StockQty > 0 && variant.StockQty <= 5))
            .ToListAsync(cancellationToken);

        var price = variants.Count > 0 ? variants.Min(variant => variant.Price) : product.BasePrice;
        return new ProductDetailsDto(product.Id, product.ProductCode, product.Name, product.Description,
            product.CategoryName, price, images, variants)
        {
            AverageRating = product.AverageRating is null ? null : decimal.Round(product.AverageRating.Value, 1),
            ReviewCount = product.ReviewCount
        };
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.AsNoTracking().OrderBy(category => category.Name)
            .Select(category => new CategoryListItemDto(
                category.CategoryId,
                category.ParentCategoryId,
                category.Name,
                db.Products.Count(product => product.IsActive && product.CategoryId == category.CategoryId)))
            .ToListAsync(cancellationToken);

    public async Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync("product-browse:filter-options", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FilterCacheDuration;
            var activeVariants = db.ProductVariants.AsNoTracking().Where(variant => variant.Product.IsActive);
            var colors = await activeVariants.Select(variant => variant.Color.Name).Distinct().OrderBy(color => color)
                .ToListAsync(cancellationToken);
            var sizes = await activeVariants.Select(variant => variant.Size.Label).Distinct().OrderBy(size => size)
                .ToListAsync(cancellationToken);
            var variantPrices = await activeVariants
                .Select(variant => variant.PriceOverride ?? variant.Product.BasePrice)
                .ToListAsync(cancellationToken);
            var pricesWithoutVariants = await db.Products.AsNoTracking()
                .Where(product => product.IsActive && !product.Variants.Any())
                .Select(product => product.BasePrice)
                .ToListAsync(cancellationToken);
            var prices = variantPrices.Concat(pricesWithoutVariants).ToArray();
            return new ProductFilterOptionsDto(colors, sizes,
                prices.Length > 0 ? prices.Min() : null,
                prices.Length > 0 ? prices.Max() : null);
        }) ?? new ProductFilterOptionsDto([], [], null, null);

    private sealed record ProductCoreRow(
        int Id,
        string ProductCode,
        string Name,
        string? Description,
        string CategoryName,
        decimal BasePrice,
        decimal? AverageRating,
        int ReviewCount);

    private sealed record ProductImageRow(int ProductId, string Url, bool IsPrimary, int SortOrder);

    private sealed record ProductVariantRow(int ProductId, string Color, string Size, decimal Price, int StockQty);
}
