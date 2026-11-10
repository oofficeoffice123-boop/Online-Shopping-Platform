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

        await AddMissingAsync(db.Roles,
            new[] { Roles.Customer, Roles.Manager, Roles.Admin }.Select(name => new Role { Name = name }),
            x => x.Name, cancellationToken);
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
