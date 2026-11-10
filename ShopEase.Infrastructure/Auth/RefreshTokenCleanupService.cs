using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopEase.Application.Auth;

namespace ShopEase.Infrastructure.Auth;

public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var deleted = await repository.DeleteExpiredBeforeAsync(cutoff, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} refresh tokens expired more than 30 days ago.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Refresh token cleanup failed.");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }
    }
}
