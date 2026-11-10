using System.Security.Cryptography;
using System.Text;
using ShopEase.Application.Auth;
using ShopEase.Domain;

namespace ShopEase.Infrastructure.Auth;

public sealed class RefreshTokenService(IRefreshTokenRepository repository) : IRefreshTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public async Task<CreatedRefreshToken> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateToken();
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(TokenLifetime);
        await repository.AddAsync(new RefreshToken
        {
            UserId = user.UserId,
            User = user,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = expiresAt
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new CreatedRefreshToken(rawToken, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }

    public async Task<RotatedRefreshToken> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        var current = await repository.FindByHashAsync(HashToken(token), cancellationToken);
        if (current is null)
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);

        var now = DateTime.UtcNow;
        if (current.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(current.UserId, cancellationToken);
            throw new AuthException(AuthException.ReusedRefreshTokenMessage);
        }

        if (current.ExpiresAt <= now)
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);

        if (!current.User.IsActive)
        {
            await RevokeAllForUserAsync(current.UserId, cancellationToken);
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);
        }

        var nextRawToken = GenerateToken();
        var nextHash = HashToken(nextRawToken);
        var nextExpiresAt = now.Add(TokenLifetime);
        current.RevokedAt = now;
        current.ReplacedByTokenHash = nextHash;
        await repository.AddAsync(new RefreshToken
        {
            UserId = current.UserId,
            User = current.User,
            TokenHash = nextHash,
            CreatedAt = now,
            ExpiresAt = nextExpiresAt
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new RotatedRefreshToken(
            current.User,
            nextRawToken,
            new DateTimeOffset(nextExpiresAt, TimeSpan.Zero));
    }

    public async Task RevokeAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var current = await repository.FindByHashAsync(HashToken(token), cancellationToken);
        if (current is null || current.RevokedAt is not null)
            return;

        current.RevokedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        repository.RevokeAllForUserAsync(userId, DateTime.UtcNow, cancellationToken);
}
