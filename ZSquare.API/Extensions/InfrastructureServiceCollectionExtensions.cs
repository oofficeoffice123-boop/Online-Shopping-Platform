using Microsoft.EntityFrameworkCore;
using ZSquare.Application.Auth;
using ZSquare.Application.Commerce;
using ZSquare.Application.Search;
using ZSquare.Infrastructure;
using ZSquare.Infrastructure.Auth;
using ZSquare.Infrastructure.Commerce;
using ZSquare.Infrastructure.Import;
using ZSquare.Infrastructure.Search;

namespace ZSquare.API.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.")));

        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<KhaadiImporter>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IUserPasswordHasher, UserPasswordHasher>();
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<DevelopmentUserSeeder>();
        services.AddHostedService<RefreshTokenCleanupService>();
        services.AddHttpClient<AiProductFilterInterpreter>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IProductFilterInterpreter>(provider => provider.GetRequiredService<AiProductFilterInterpreter>());
        services.AddScoped<ISearchCatalogProvider, CachedSearchCatalogProvider>();
        services.AddScoped<IProductSearchService, ProductSearchService>();
        services.AddScoped<IProductBrowsingService, ProductSearchService>();
        services.AddScoped<ICustomerCommerceService, CustomerCommerceService>();

        return services;
    }
}
