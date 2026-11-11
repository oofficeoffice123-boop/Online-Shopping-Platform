using System.Security.Claims;

namespace ZSquare.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("UserId");
        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("The authenticated principal has no valid user identifier.");
    }
}
