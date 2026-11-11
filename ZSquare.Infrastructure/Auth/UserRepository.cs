using Microsoft.EntityFrameworkCore;
using ZSquare.Application.Auth;
using ZSquare.Domain;
namespace ZSquare.Infrastructure.Auth;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        db.Users.Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);

    public Task<Role?> FindRoleByNameAsync(string roleName, CancellationToken cancellationToken = default) =>
        db.Roles.SingleOrDefaultAsync(role => role.Name == roleName, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
