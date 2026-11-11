using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ZSquare.Application.Search;
namespace ZSquare.Infrastructure.Search;

public sealed class CachedSearchCatalogProvider(AppDbContext db, IMemoryCache cache) : ISearchCatalogProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<SearchCatalogValues> GetValuesAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync("smart-search:catalog-values", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var categories = await db.Categories.AsNoTracking().OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(cancellationToken);
            var colors = await db.Colors.AsNoTracking().OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(cancellationToken);
            var sizes = await db.Sizes.AsNoTracking().OrderBy(x => x.Label).Select(x => x.Label).ToListAsync(cancellationToken);
            return new SearchCatalogValues(categories, colors, sizes);
        }) ?? new SearchCatalogValues([], [], []);
    }
}
