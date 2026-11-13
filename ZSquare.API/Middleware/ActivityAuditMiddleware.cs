using System.Text.Json;
using ZSquare.Domain;
using ZSquare.Infrastructure;

namespace ZSquare.API.Middleware;

public sealed class ActivityAuditMiddleware(
    RequestDelegate next,
    ILogger<ActivityAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/api/admin/audit-logs") ||
            context.User.Identity?.IsAuthenticated != true ||
            !int.TryParse(context.User.FindFirst("UserId")?.Value, out var actorUserId))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            // Some management actions already write a detailed audit event.
            // Keep those records instead of adding a duplicate request event.
            if (!db.ChangeTracker.Entries<AuditLog>().Any())
            {
                try
                {
                    var method = context.Request.Method.ToUpperInvariant();
                    var role = context.User.FindFirst("roles")?.Value;
                    db.AuditLogs.Add(new AuditLog
                    {
                        ActorUserId = actorUserId,
                        Action = $"activity.{method.ToLowerInvariant()}",
                        EntityType = "Activity",
                        DetailsJson = JsonSerializer.Serialize(new
                        {
                            method,
                            path = path.Value,
                            statusCode = context.Response.StatusCode,
                            role
                        }),
                        CreatedAt = ZSquare.Domain.PakistanClock.Now
                    });
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Could not record API activity for user {UserId} on {Path}", actorUserId, path);
                }
            }
        }
    }
}
