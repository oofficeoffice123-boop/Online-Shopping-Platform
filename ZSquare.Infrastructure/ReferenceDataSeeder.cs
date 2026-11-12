using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZSquare.Application;
using ZSquare.Domain;
namespace ZSquare.Infrastructure;

public sealed class ReferenceDataSeeder(AppDbContext db, ILogger<ReferenceDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken) ||
            !(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogInformation("Reference data seed skipped because the database has no applied migration.");
            return;
        }

        await SeedPageAccessAsync(cancellationToken);

        await AddMissingAsync(db.OrderStatuses,
            new[] { "Pending", "Paid", "Shipped", "Delivered", "Cancelled" }.Select(name => new OrderStatus { Name = name }),
            x => x.Name, cancellationToken);
        await AddMissingAsync(db.Sizes,
            new[] { "XS", "S", "M", "L", "XL", "XXL", "One Size" }.Select(label => new Size { Label = label }),
            x => x.Label, cancellationToken);
        await AddMissingAsync(db.Colors,
            new[]
            {
                "Beige", "Black", "Blue", "Brown", "Coral", "Dark Green", "Dark Pink", "Green",
                "Grey", "Light Green", "Lilac", "Light Pink", "Light Yellow", "Maroon", "Multi",
                "Mustard", "Navy", "Off-White", "Olive", "Orange", "Pink", "Purple", "Red", "Rust",
                "Teal", "Turquoise", "White", "Yellow"
            }.Select(name => new Color { Name = name }),
            x => x.Name, cancellationToken);
        await AddMissingAsync(db.Categories,
            new[] { "Unstitched Fabric", "Kurtas and Kameez", "Shirts and Tops", "Dresses", "Bottoms", "Dupattas and Shawls" }
                .Select(name => new Category { Name = name }),
            x => x.Name, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Reference data seeded.");
    }

    public async Task SeedPageAccessAsync(CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken) ||
            !(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogInformation("Page access seed skipped because the database has no applied migration.");
            return;
        }

        await AddMissingAsync(db.Roles,
            new[] { Roles.Customer, Roles.Manager, Roles.Admin }.Select(name => new Role { Name = name }),
            x => x.Name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var pages = new[]
        {
            new Page { Name = "Shop", Route = "/products", SortOrder = 10 },
            new Page { Name = "Cart", Route = "/cart", SortOrder = 20 },
            new Page { Name = "Checkout", Route = "/checkout", SortOrder = 25 },
            new Page { Name = "Orders", Route = "/orders", SortOrder = 30 },
            new Page { Name = "Manager Dashboard", Route = "/manage/dashboard", SortOrder = 40 },
            new Page { Name = "Inventory", Route = "/manage/inventory", SortOrder = 50 },
            new Page { Name = "Manage Products", Route = "/manage", SortOrder = 60 },
            new Page { Name = "Review Moderation", Route = "/manage/reviews", SortOrder = 70 },
            new Page { Name = "User Management", Route = "/admin/users", SortOrder = 80 },
            new Page { Name = "Audit Log", Route = "/admin/audit-logs", SortOrder = 90 }
        };
        await AddMissingAsync(db.Pages, pages, x => x.Route, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var roleIds = await db.Roles.AsNoTracking().ToDictionaryAsync(x => x.Name, x => x.RoleId, cancellationToken);
        var pageIds = await db.Pages.AsNoTracking().ToDictionaryAsync(x => x.Route, x => x.PageId, cancellationToken);
        var rolePageMappings = new[]
        {
            (Roles.Customer, "/products"), (Roles.Customer, "/cart"), (Roles.Customer, "/checkout"), (Roles.Customer, "/orders"),
            (Roles.Manager, "/manage/dashboard"), (Roles.Manager, "/manage/inventory"),
            (Roles.Manager, "/manage"), (Roles.Manager, "/manage/reviews"),
            (Roles.Admin, "/products"), (Roles.Admin, "/cart"), (Roles.Admin, "/checkout"), (Roles.Admin, "/orders"),
            (Roles.Admin, "/manage/dashboard"), (Roles.Admin, "/manage/inventory"),
            (Roles.Admin, "/manage"), (Roles.Admin, "/manage/reviews"),
            (Roles.Admin, "/admin/users"), (Roles.Admin, "/admin/audit-logs")
        }.Select(pair => new RolePage { RoleId = roleIds[pair.Item1], PageId = pageIds[pair.Item2] }).ToArray();
        var existingRolePages = await db.RolePages.AsNoTracking().Select(x => new { x.RoleId, x.PageId }).ToListAsync(cancellationToken);
        var existingRolePageKeys = existingRolePages.Select(x => (x.RoleId, x.PageId)).ToHashSet();
        db.RolePages.AddRange(rolePageMappings.Where(x => !existingRolePageKeys.Contains((x.RoleId, x.PageId))));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Role page access seeded.");
    }

    private async Task AddMissingAsync<TEntity, TKey>(
        DbSet<TEntity> set,
        IEnumerable<TEntity> expected,
        Func<TEntity, TKey> keySelector,
        CancellationToken cancellationToken)
        where TEntity : class
        where TKey : notnull
    {
        var existing = (await set.ToListAsync(cancellationToken)).Select(keySelector).ToHashSet();
        set.AddRange(expected.Where(entity => !existing.Contains(keySelector(entity))));
    }
}
