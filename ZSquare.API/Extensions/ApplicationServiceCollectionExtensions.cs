using FluentValidation;
using ZSquare.Application.Auth;
using ZSquare.Application.Commerce;
using ZSquare.Application.Search;

namespace ZSquare.API.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<AddressRequest>, AddressRequestValidator>();
        services.AddScoped<IValidator<AddCartItemRequest>, AddCartItemRequestValidator>();
        services.AddScoped<IValidator<SetCartItemQuantityRequest>, SetCartItemQuantityRequestValidator>();
        services.AddScoped<IValidator<CheckoutRequest>, CheckoutRequestValidator>();
        services.AddScoped<ISmartSearchService, SmartSearchService>();
        services.AddSingleton<SmartSearchFilterParser>();

        return services;
    }
}
