using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ZSquare.API.Middleware;
using ZSquare.API.Controllers;
using ZSquare.Application;
using ZSquare.Application.Auth;
using ZSquare.Application.Commerce;
using ZSquare.Application.Search;
using ZSquare.Infrastructure;
using ZSquare.Infrastructure.Auth;
using ZSquare.Infrastructure.Commerce;
using ZSquare.Infrastructure.Import;
using ZSquare.Infrastructure.Search;

var importMode = args.Any(argument => argument.Equals("import", StringComparison.OrdinalIgnoreCase) ||
                                      argument.Equals("--import", StringComparison.OrdinalIgnoreCase));
var builder = WebApplication.CreateBuilder(args.Where(argument =>
    !argument.Equals("import", StringComparison.OrdinalIgnoreCase) &&
    !argument.Equals("--import", StringComparison.OrdinalIgnoreCase)).ToArray());

builder.Host.UseSerilog((_, logger) => logger.WriteTo.Console());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
var reactOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
if (reactOrigins.Length == 0 || reactOrigins.Any(origin => string.IsNullOrWhiteSpace(origin) || origin == "*"))
    throw new InvalidOperationException("Configure one or more explicit React origins in Cors:AllowedOrigins; wildcard origins are not allowed.");
builder.Services.AddCors(options => options.AddPolicy("React", policy =>
    policy.WithOrigins(reactOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddRateLimiter(options =>
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
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ZSquare.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ZSquare.ReactClient";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 bytes and be provided through user-secrets or an environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffOnly", policy => policy.RequireRole(Roles.Manager, Roles.Admin));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole(Roles.Customer));
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.")));
builder.Services.AddScoped<ReferenceDataSeeder>();
builder.Services.AddScoped<KhaadiImporter>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddSingleton<IUserPasswordHasher, UserPasswordHasher>();
builder.Services.AddScoped<IAccessTokenService, AccessTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<AddressRequest>, AddressRequestValidator>();
builder.Services.AddScoped<IValidator<AddCartItemRequest>, AddCartItemRequestValidator>();
builder.Services.AddScoped<IValidator<SetCartItemQuantityRequest>, SetCartItemQuantityRequestValidator>();
builder.Services.AddScoped<IValidator<CheckoutRequest>, CheckoutRequestValidator>();
builder.Services.AddScoped<IValidator<ProductRequest>, ProductRequestValidator>();
builder.Services.AddScoped<IValidator<ActiveRequest>, ActiveRequestValidator>();
builder.Services.AddScoped<IValidator<VariantCreateRequest>, VariantCreateRequestValidator>();
builder.Services.AddScoped<IValidator<VariantUpdateRequest>, VariantUpdateRequestValidator>();
builder.Services.AddScoped<IValidator<StockRequest>, StockRequestValidator>();
builder.Services.AddScoped<IValidator<StatusRequest>, StatusRequestValidator>();
builder.Services.AddScoped<IValidator<ImageOrderRequest>, ImageOrderRequestValidator>();
builder.Services.AddScoped<IValidator<CategoryRequest>, CategoryRequestValidator>();
builder.Services.AddScoped<IValidator<NameRequest>, NameRequestValidator>();
builder.Services.AddScoped<IValidator<ColorRequest>, ColorRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStaffRequest>, CreateStaffRequestValidator>();
builder.Services.AddScoped<IValidator<ChangeUserRoleRequest>, ChangeUserRoleRequestValidator>();
builder.Services.AddScoped<IValidator<SetUserActiveRequest>, SetUserActiveRequestValidator>();
builder.Services.AddScoped<IValidator<ReviewWriteRequest>, ReviewWriteRequestValidator>();
builder.Services.AddScoped<IValidator<SetReviewHiddenRequest>, SetReviewHiddenRequestValidator>();
builder.Services.AddScoped<DevelopmentUserSeeder>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();
builder.Services.AddHttpClient<AiProductFilterInterpreter>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IProductFilterInterpreter>(services => services.GetRequiredService<AiProductFilterInterpreter>());
builder.Services.AddScoped<ISearchCatalogProvider, CachedSearchCatalogProvider>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
builder.Services.AddScoped<IProductBrowsingService, ProductSearchService>();
builder.Services.AddScoped<ICustomerCommerceService, CustomerCommerceService>();
builder.Services.AddScoped<ISmartSearchService, SmartSearchService>();
builder.Services.AddSingleton<SmartSearchFilterParser>();

var app = builder.Build();

if (importMode)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.Database.CanConnectAsync())
        throw new InvalidOperationException("Cannot connect to the database. Review and apply the initial migration, then run the importer again.");

    var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
    var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
    if (!appliedMigrations.Any() || pendingMigrations.Any())
        throw new InvalidOperationException("The database schema is not up to date. Review and apply the migration before importing.");

    await scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>().SeedAsync();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var options = new KhaadiImportOptions(
        configuration["Import:ExcelPath"] ?? throw new InvalidOperationException("Import:ExcelPath is not configured."),
        configuration["Import:ImagesSourcePath"] ?? throw new InvalidOperationException("Import:ImagesSourcePath is not configured."),
        Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
    var summary = await scope.ServiceProvider.GetRequiredService<KhaadiImporter>().ImportAsync(options);

    Console.WriteLine("Khaadi import summary:");
    Console.WriteLine($"Products inserted: {summary.ProductsInserted}");
    Console.WriteLine($"Products skipped: {summary.ProductsSkipped}");
    Console.WriteLine($"Variants created: {summary.VariantsCreated}");
    Console.WriteLine($"Images created: {summary.ImagesCreated}");
    Console.WriteLine($"Unmatched categories: {summary.UnmatchedCategories}");
    Console.WriteLine($"Image folders missing: {summary.MissingImageFolders}");
    Console.WriteLine($"Image folders empty: {summary.EmptyImageFolders}");
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("React");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var needsPasswordChange = context.User.Identity?.IsAuthenticated == true &&
        context.User.FindFirst("must_change_password")?.Value == "true";
    var allowed = context.Request.Path.StartsWithSegments("/api/auth/change-password") ||
        context.Request.Path.StartsWithSegments("/api/auth/logout") ||
        context.Request.Path.StartsWithSegments("/api/auth/me");
    if (needsPasswordChange && !allowed)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Password change required.",
            Detail = "Change the temporary password before using the application."
        });
        return;
    }
    await next();
});
app.UseAuthorization();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ReferenceDataSeeder>();
    await seeder.SeedAsync();
    await scope.ServiceProvider.GetRequiredService<DevelopmentUserSeeder>().SeedAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.MapPost("/api/search/smart", async (SmartSearchRequestDto request, ISmartSearchService search, CancellationToken cancellationToken) =>
{
    var query = request.Query?.Trim();
    if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Query)] = ["Query is required and must be at most 200 characters."]
        });

    return Results.Ok(await search.SearchAsync(query, cancellationToken));
})
    .AllowAnonymous()
    .RequireRateLimiting("SmartSearch")
    .WithTags("Search");

app.MapGet("/api/search/products", async (
    string? category,
    decimal? minPrice,
    decimal? maxPrice,
    decimal? minRating,
    string? color,
    string? keyword,
    string? size,
    string? sort,
    int? page,
    int? pageSize,
    IProductSearchService search,
    CancellationToken cancellationToken) =>
{
    var filterErrors = new Dictionary<string, string[]>();
    if (minPrice is < 0 or > 1_000_000 || maxPrice is < 0 or > 1_000_000 ||
        (minPrice is not null && maxPrice is not null && minPrice > maxPrice))
        filterErrors["price"] = ["Prices must be between 0 and 1000000 PKR, and minimum cannot exceed maximum."];
    if (minRating is < 1 or > 5)
        filterErrors["minRating"] = ["Minimum rating must be between 1 and 5."];
    if (filterErrors.Count > 0) return Results.ValidationProblem(filterErrors);

    var filters = new ProductFilterDto
    {
        Category = category?.Trim(),
        MinPrice = minPrice,
        MaxPrice = maxPrice,
        MinRating = minRating,
        Color = color?.Trim(),
        Keyword = keyword?.Trim(),
        Size = size?.Trim(),
        Sort = sort?.Trim(),
        Page = page ?? 1,
        PageSize = pageSize ?? 20
    };
    return Results.Ok(await search.SearchAsync(filters, cancellationToken));
})
    .AllowAnonymous()
    .WithTags("Search");

app.MapControllers();

app.Run();
