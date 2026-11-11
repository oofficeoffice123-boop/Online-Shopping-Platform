using ZSquare.Application;
using ZSquare.Domain;
namespace ZSquare.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    IUserPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokens) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
            throw new EmailAlreadyRegisteredException();

        var customerRole = await users.FindRoleByNameAsync(Roles.Customer, cancellationToken)
            ?? throw new InvalidOperationException("Customer role is missing. Run reference-data seeding first.");
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = customerRole.RoleId,
            Role = customerRole,
            IsActive = true,
            CreatedAt = PakistanClock.Now
        };
        user.PasswordHash = passwordHasher.Hash(user, request.Password);
        await users.AddAsync(user, cancellationToken);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null || user.IsDeleted || !user.IsActive || !passwordHasher.Verify(user, request.Password))
            throw new AuthException(AuthException.InvalidCredentialsMessage);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new AuthException(AuthException.InvalidRefreshTokenMessage);

        var rotated = await refreshTokens.RotateAsync(request.Token, cancellationToken);
        var accessToken = accessTokens.Generate(rotated.User);
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            ToSummary(rotated.User),
            rotated.Token,
            rotated.ExpiresAt);
    }

    public Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default) =>
        refreshTokens.RevokeAsync(request.Token, cancellationToken);

    public async Task<MeResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted || !user.IsActive)
            throw new AuthException("The account is not available.");

        return new MeResponse(user.UserId, user.FullName, user.Email, user.Role.Name);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = await refreshTokens.CreateAsync(user, cancellationToken);
        var accessToken = accessTokens.Generate(user);
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            ToSummary(user),
            refreshToken.Token,
            refreshToken.ExpiresAt);
    }

    private static AuthUserSummary ToSummary(User user) =>
        new(user.UserId, user.FullName, user.Email, user.Role.Name);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
