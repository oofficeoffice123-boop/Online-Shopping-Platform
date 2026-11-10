using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ZSquare.Application.Auth;
using ZSquare.API.Security;

namespace ZSquare.API.Controllers;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : ControllerBase
{
    private const string RefreshCookieName = "ZSquare.RefreshToken";

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthRegister")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!await ValidateAsync(request, registerValidator, cancellationToken))
            return ValidationProblem(ModelState);

        try
        {
            var response = await authService.RegisterAsync(request, cancellationToken);
            SetRefreshCookie(response);
            return Ok(response);
        }
        catch (EmailAlreadyRegisteredException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = exception.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthLogin")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!await ValidateAsync(request, loginValidator, cancellationToken))
            return ValidationProblem(ModelState);

        try
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            SetRefreshCookie(response);
            return Ok(response);
        }
        catch (AuthException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var request = new RefreshRequest(Request.Cookies[RefreshCookieName]);
            var response = await authService.RefreshAsync(request, cancellationToken);
            SetRefreshCookie(response);
            return Ok(response);
        }
        catch (AuthException exception)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(new RefreshRequest(Request.Cookies[RefreshCookieName]), cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken));
        }
        catch (AuthException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request,
        [FromServices] ZSquare.Infrastructure.AppDbContext db,
        [FromServices] IUserPasswordHasher hasher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["newPassword"] = ["Current password is required and the new password must be at least 12 characters."] }));
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == User.GetUserId(), cancellationToken);
        if (user is null || !hasher.Verify(user, request.CurrentPassword))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Current password is incorrect." });
        user.PasswordHash = hasher.Hash(user, request.NewPassword);
        user.MustChangePassword = false;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.UserId && t.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    private async Task<bool> ValidateAsync<T>(T request, IValidator<T> validator, CancellationToken cancellationToken)
    {
        ValidationResult result = await validator.ValidateAsync(request, cancellationToken);
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return result.IsValid;
    }

    private void SetRefreshCookie(AuthResponse response)
    {
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = response.RefreshTokenExpiresAt,
            IsEssential = true
        });
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    });
}
