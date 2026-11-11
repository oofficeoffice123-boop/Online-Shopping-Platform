using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZSquare.Application;
using ZSquare.Application.Auth;
using ZSquare.Domain;

namespace ZSquare.Infrastructure.Auth;

public sealed class DevelopmentUserSeeder(
    AppDbContext db,
    IConfiguration configuration,
    IUserPasswordHasher passwordHasher,
    ILogger<DevelopmentUserSeeder> logger)
{
    private static readonly (string Role, string Name, string Email, string PasswordKey)[] DevelopmentUsers =
    [
        (Roles.Admin, "Z SQUARE Admin", "admin@zsquare.local", "Auth:SeedPasswords:Admin"),
        (Roles.Manager, "Z SQUARE Manager", "manager@zsquare.local", "Auth:SeedPasswords:Manager"),
        (Roles.Customer, "Z SQUARE Customer", "customer@zsquare.local", "Auth:SeedPasswords:Customer")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken) ||
            !(await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogInformation("Development user seeding skipped because the database has no applied migration.");
            return;
        }

        var roles = await db.Roles.ToDictionaryAsync(role => role.Name, cancellationToken);
        foreach (var (roleName, fullName, email, passwordKey) in DevelopmentUsers)
        {
            var legacyEmail = email.Replace("@zsquare.local", "@shopease.local", StringComparison.Ordinal);
            var existingUser = await db.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken)
                ?? await db.Users.SingleOrDefaultAsync(user => user.Email == legacyEmail, cancellationToken);
            if (existingUser is not null)
            {
                if (existingUser.Email != email || existingUser.FullName != fullName)
                {
                    existingUser.Email = email;
                    existingUser.FullName = fullName;
                }
                continue;
            }

            var password = configuration[passwordKey];
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException($"Missing development-user password setting '{passwordKey}'. Set it with dotnet user-secrets before starting in Development.");

            if (!roles.TryGetValue(roleName, out var role))
                throw new InvalidOperationException($"Role '{roleName}' is missing. Run reference-data seeding first.");

            var user = new User
            {
                FullName = fullName,
                Email = email,
                RoleId = role.RoleId,
                Role = role,
                IsActive = true,
                CreatedAt = PakistanClock.Now
            };
            user.PasswordHash = passwordHasher.Hash(user, password);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development users seeded when missing.");
    }
}
