using System.Text.Json.Serialization;
using ShopEase.Domain;

namespace ShopEase.Application.Auth;

public sealed class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record RefreshRequest(string? Token);

public sealed record AuthUserSummary(int UserId, string FullName, string Email, string Role, bool MustChangePassword);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    AuthUserSummary User,
    [property: JsonIgnore] string RefreshToken,
    [property: JsonIgnore] DateTimeOffset RefreshTokenExpiresAt);

public sealed record MeResponse(int UserId, string FullName, string Email, string Role);

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed record CreatedRefreshToken(string Token, DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(User User, string Token, DateTimeOffset ExpiresAt);

public sealed class AuthException(string message) : Exception(message)
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string InvalidRefreshTokenMessage = "The refresh token is invalid or expired.";
    public const string ReusedRefreshTokenMessage = "The refresh token has already been used. Please log in again.";
}

public sealed class EmailAlreadyRegisteredException() : Exception("An account with this email already exists.");

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<MeResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IAccessTokenService
{
    AccessTokenResult Generate(User user);
}

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
    Task<CreatedRefreshToken> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<RotatedRefreshToken> RotateAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(string? token, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IUserPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<Role?> FindRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(int userId, DateTime revokedAt, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
