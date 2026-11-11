using System.Security.Cryptography;
using System.Text;
using ZSquare.Application.Auth;
using ZSquare.Domain;

namespace ZSquare.Infrastructure.Auth;

public sealed class RefreshTokenService(IRefreshTokenRepository repository) : IRefreshTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public async Task<CreatedRefreshToken> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateToken();
        var now = PakistanClock.Now;
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
        return new CreatedRefreshToken(rawToken, new DateTimeOffset(expiresAt, TimeSpan.FromHours(5)));
    }

    public async Task<RotatedRefreshToken> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        var current = await repository.FindByHashAsync(HashToken(token), cancellationToken);
        if (current is null)
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);

        var now = PakistanClock.Now;
        if (current.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(current.UserId, cancellationToken);
            throw new AuthException(AuthException.ReusedRefreshTokenMessage);
        }

        if (current.ExpiresAt <= now)
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);

        if (current.User.IsDeleted || !current.User.IsActive)
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
            new DateTimeOffset(nextExpiresAt, TimeSpan.FromHours(5)));
    }

    public async Task RevokeAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var current = await repository.FindByHashAsync(HashToken(token), cancellationToken);
        if (current is null || current.RevokedAt is not null)
            return;

        current.RevokedAt = PakistanClock.Now;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        repository.RevokeAllForUserAsync(userId, PakistanClock.Now, cancellationToken);
}
