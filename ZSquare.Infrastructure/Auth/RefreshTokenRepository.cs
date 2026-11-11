using Microsoft.EntityFrameworkCore;
using ZSquare.Application.Auth;
using ZSquare.Domain;
namespace ZSquare.Infrastructure.Auth;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.Include(token => token.User)
            .ThenInclude(user => user.Role)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        db.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task<int> RevokeAllForUserAsync(int userId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var activeTokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
            token.RevokedAt = revokedAt;

        await db.SaveChangesAsync(cancellationToken);
        return activeTokens.Count;
    }

    public Task<int> DeleteExpiredBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.Where(token => token.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
}
