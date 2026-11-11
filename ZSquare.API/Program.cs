using Microsoft.EntityFrameworkCore;
using Serilog;
using ZSquare.API.Extensions;
using ZSquare.API.Middleware;
using ZSquare.Application.Search;
using ZSquare.Infrastructure;
using ZSquare.Infrastructure.Auth;
using ZSquare.Infrastructure.Import;

var importMode = args.Any(argument => argument.Equals("import", StringComparison.OrdinalIgnoreCase) ||
                                      argument.Equals("--import", StringComparison.OrdinalIgnoreCase));
var builder = WebApplication.CreateBuilder(args.Where(argument =>
    !argument.Equals("import", StringComparison.OrdinalIgnoreCase) &&
    !argument.Equals("--import", StringComparison.OrdinalIgnoreCase)).ToArray());

builder.Host.UseSerilog((_, logger) => logger.WriteTo.Console());
builder.Services
    .AddApiServices(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

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
