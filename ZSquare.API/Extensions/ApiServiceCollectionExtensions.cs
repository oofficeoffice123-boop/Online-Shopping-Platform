using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ZSquare.API.Controllers;
using ZSquare.Application;
using ZSquare.Application.Auth;
using ZSquare.Application.Commerce;
using ZSquare.Infrastructure;

namespace ZSquare.API.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddMemoryCache();
        services.AddReactCors(configuration);
        services.AddApiRateLimiting();
        services.AddJwtAuthentication(configuration);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("StaffOnly", policy => policy.RequireRole(Roles.Manager, Roles.Admin));
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy("CustomerOnly", policy => policy.RequireRole(Roles.Customer));
            options.AddPolicy("CustomerOrAdmin", policy => policy.RequireRole(Roles.Customer, Roles.Admin));
        });
        services.AddApiValidators();

        return services;
    }

    private static IServiceCollection AddReactCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
        if (origins.Length == 0 || origins.Any(origin => string.IsNullOrWhiteSpace(origin) || origin == "*"))
            throw new InvalidOperationException("Configure one or more explicit React origins in Cors:AllowedOrigins; wildcard origins are not allowed.");

        services.AddCors(options => options.AddPolicy("React", policy =>
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }

    private static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("SmartSearch", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("AuthLogin", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("AuthRegister", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "ZSquare.API";
        var audience = configuration["Jwt:Audience"] ?? "ZSquare.ReactClient";
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes and be provided through user-secrets or an environment variable.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "UserId",
                    RoleClaimType = "roles"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirst("UserId")?.Value;
                        if (!int.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("The access token does not identify a valid user.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var accountAvailable = await db.Users.AsNoTracking().AnyAsync(
                            user => user.UserId == userId && user.IsActive && !user.IsDeleted,
                            context.HttpContext.RequestAborted);
                        if (!accountAvailable)
                            context.Fail("The user account is inactive or deleted.");
                    }
                };
            });

        return services;
    }

    private static IServiceCollection AddApiValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<ProductRequest>, ProductRequestValidator>();
        services.AddScoped<IValidator<ActiveRequest>, ActiveRequestValidator>();
        services.AddScoped<IValidator<VariantCreateRequest>, VariantCreateRequestValidator>();
        services.AddScoped<IValidator<VariantUpdateRequest>, VariantUpdateRequestValidator>();
        services.AddScoped<IValidator<StockRequest>, StockRequestValidator>();
        services.AddScoped<IValidator<StatusRequest>, StatusRequestValidator>();
        services.AddScoped<IValidator<ImageOrderRequest>, ImageOrderRequestValidator>();
        services.AddScoped<IValidator<CategoryRequest>, CategoryRequestValidator>();
        services.AddScoped<IValidator<NameRequest>, NameRequestValidator>();
        services.AddScoped<IValidator<ColorRequest>, ColorRequestValidator>();
        services.AddScoped<IValidator<CreateAdminUserRequest>, CreateAdminUserRequestValidator>();
        services.AddScoped<IValidator<UpdateAdminUserRequest>, UpdateAdminUserRequestValidator>();
        services.AddScoped<IValidator<ChangeAdminPasswordRequest>, ChangeAdminPasswordRequestValidator>();
        services.AddScoped<IValidator<ReviewWriteRequest>, ReviewWriteRequestValidator>();
        services.AddScoped<IValidator<SetReviewHiddenRequest>, SetReviewHiddenRequestValidator>();

        return services;
    }
}
