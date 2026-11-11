using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZSquare.Application.Search;
using ZSquare.Domain;
using System.Globalization;

namespace ZSquare.Infrastructure.Import;

public sealed record KhaadiImportOptions(string ExcelPath, string ImagesSourcePath, string WebRootPath);

public sealed record KhaadiProductRow(
    string ProductCode,
    string Name,
    string? Description,
    decimal Price,
    string Color);

public sealed record KhaadiImportSummary(
    int ProductsInserted,
    int ProductsSkipped,
    int VariantsCreated,
    int ImagesCreated,
    int UnmatchedCategories,
    int MissingImageFolders,
    int EmptyImageFolders);

public sealed class KhaadiImporter(AppDbContext db, ILogger<KhaadiImporter> logger)
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"
    };

    public async Task<KhaadiImportSummary> ImportAsync(
        KhaadiImportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.ExcelPath))
            throw new FileNotFoundException("Khaadi Excel file was not found.", options.ExcelPath);

        var rows = ReadRows(options.ExcelPath);
        return await ImportRowsAsync(rows, options.ImagesSourcePath, options.WebRootPath, cancellationToken);
    }

    public async Task<KhaadiImportSummary> ImportRowsAsync(
        IReadOnlyCollection<KhaadiProductRow> rows,
        string imagesSourcePath,
        string webRootPath,
        CancellationToken cancellationToken = default)
    {
        var existingCodes = (await db.Products.AsNoTracking().Select(x => x.ProductCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categories = (await db.Categories.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var sizes = (await db.Sizes.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Label, StringComparer.OrdinalIgnoreCase);
        var colors = (await db.Colors.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var skipped = 0;
        var variantsCreated = 0;
        var imagesCreated = 0;
        var unmatched = 0;
        var missingFolders = 0;
        var emptyFolders = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var productCode = row.ProductCode.Trim();
            var categoryName = KhaadiCatalogMapping.MapProductCategory(row.Name);
            if (categoryName is null || !categories.TryGetValue(categoryName, out var category))
            {
                unmatched++;
                logger.LogWarning("No category matched product row {ProductCode}: {ProductName}", productCode, row.Name);
                continue;
            }

            if (existingCodes.Contains(productCode))
            {
                skipped++;
                continue;
            }

            if (row.Price is < 990m or > 39_990m)
                logger.LogWarning("Price outside the observed Khaadi data range for product {ProductCode}: {Price} PKR", productCode, row.Price);

            if (string.IsNullOrWhiteSpace(productCode) || productCode.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                productCode.Contains("..", StringComparison.Ordinal) || productCode.Contains('/', StringComparison.Ordinal) ||
                productCode.Contains('\\', StringComparison.Ordinal))
                throw new InvalidDataException($"Invalid product code '{row.ProductCode}'.");

            var normalizedColor = KhaadiCatalogMapping.NormalizeColor(row.Color);
            if (string.IsNullOrWhiteSpace(normalizedColor))
                throw new InvalidDataException($"Product {productCode} has an empty color.");

            if (!colors.TryGetValue(normalizedColor, out var color))
            {
                color = new Color { Name = normalizedColor };
                db.Colors.Add(color);
                colors.Add(normalizedColor, color);
            }

            var productSizes = categoryName is "Unstitched Fabric" or "Dupattas and Shawls"
                ? new[] { "One Size" }
                : new[] { "XS", "S", "M", "L", "XL", "XXL" };
            var sourceFolder = Path.Combine(imagesSourcePath, productCode);
            var destinationFolder = Path.Combine(webRootPath, "images", productCode);
            var imageFiles = Directory.Exists(sourceFolder)
                ? Directory.EnumerateFiles(sourceFolder)
                    .Where(file => ImageExtensions.Contains(Path.GetExtension(file)))
                    .OrderBy(file => Path.GetFileName(file), NaturalFileNameComparer.Instance)
                    .ToArray()
                : [];

            if (!Directory.Exists(sourceFolder))
            {
                missingFolders++;
                logger.LogWarning("Image folder is missing for product {ProductCode}: {Folder}", productCode, sourceFolder);
            }
            else if (imageFiles.Length == 0)
            {
                emptyFolders++;
                logger.LogWarning("Image folder is empty for product {ProductCode}: {Folder}", productCode, sourceFolder);
            }

            var product = new Product
            {
                ProductCode = productCode,
                Category = category,
                Name = row.Name.Trim()[..Math.Min(row.Name.Trim().Length, 200)],
                Description = Truncate(row.Description?.Trim(), 4000),
                BasePrice = row.Price,
                IsActive = true,
                CreatedAt = PakistanClock.Now
            };

            foreach (var sizeLabel in productSizes)
            {
                if (!sizes.TryGetValue(sizeLabel, out var size))
                    throw new InvalidOperationException($"Required size '{sizeLabel}' is missing. Run reference-data seeding after applying the migration.");

                var sku = $"{productCode}-{sizeLabel.Replace(" ", string.Empty, StringComparison.Ordinal)}-{normalizedColor.Replace(" ", string.Empty, StringComparison.Ordinal)}";
                product.Variants.Add(new ProductVariant
                {
                    Size = size,
                    Color = color,
                    SKU = sku,
                    StockQty = Random.Shared.Next(0, 21)
                });
            }

            if (imageFiles.Length > 0)
            {
                Directory.CreateDirectory(destinationFolder);
                for (var index = 0; index < imageFiles.Length; index++)
                {
                    var fileName = Path.GetFileName(imageFiles[index]);
                    File.Copy(imageFiles[index], Path.Combine(destinationFolder, fileName), overwrite: true);
                    product.Images.Add(new ProductImage
                    {
                        Url = $"/images/{productCode}/{fileName.Replace('\\', '/')}",
                        IsPrimary = index == 0,
                        SortOrder = index + 1
                    });
                }
            }

            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);
            existingCodes.Add(productCode);
            inserted++;
            variantsCreated += product.Variants.Count;
            imagesCreated += product.Images.Count;
        }

        return new KhaadiImportSummary(inserted, skipped, variantsCreated, imagesCreated, unmatched, missingFolders, emptyFolders);
    }

    private static IReadOnlyList<KhaadiProductRow> ReadRows(string excelPath)
    {
        using var workbook = new XLWorkbook(excelPath);
        var worksheet = workbook.Worksheet("khaadi_data");
        var headers = worksheet.Row(1).CellsUsed()
            .ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        var requiredHeaders = new[] { "ID", "Product Name", "Product Description", "Price", "Color" };
        var missing = requiredHeaders.Where(header => !headers.ContainsKey(header)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Excel sheet is missing required columns: {string.Join(", ", missing)}.");

        var rows = new List<KhaadiProductRow>();
        foreach (var excelRow in worksheet.RowsUsed().Skip(1))
        {
            var productCode = excelRow.Cell(headers["ID"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(productCode))
                continue;

            var priceCell = excelRow.Cell(headers["Price"]);
            var price = priceCell.TryGetValue<decimal>(out var numericPrice)
                ? numericPrice
                : decimal.Parse(priceCell.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture);
            if (price < 0 || price > 9_999_999.99m)
                throw new InvalidDataException($"Product {productCode} has an invalid price: {price}.");

            rows.Add(new KhaadiProductRow(
                productCode,
                excelRow.Cell(headers["Product Name"]).GetString().Trim(),
                excelRow.Cell(headers["Product Description"]).GetString(),
                price,
                excelRow.Cell(headers["Color"]).GetString().Trim()));
        }

        return rows;
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is { Length: > 0 } ? value[..Math.Min(value.Length, maximumLength)] : null;

    private sealed class NaturalFileNameComparer : IComparer<string>
    {
        public static NaturalFileNameComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            var i = 0;
            var j = 0;
            while (i < left.Length && j < right.Length)
            {
                if (char.IsDigit(left[i]) && char.IsDigit(right[j]))
                {
                    var leftStart = i;
                    var rightStart = j;
                    while (i < left.Length && left[i] == '0') i++;
                    while (j < right.Length && right[j] == '0') j++;
                    var leftDigits = i;
                    var rightDigits = j;
                    while (i < left.Length && char.IsDigit(left[i])) i++;
                    while (j < right.Length && char.IsDigit(right[j])) j++;
                    var lengthCompare = (i - leftDigits).CompareTo(j - rightDigits);
                    if (lengthCompare != 0) return lengthCompare;
                    var numberCompare = string.Compare(left, leftDigits, right, rightDigits, i - leftDigits, StringComparison.OrdinalIgnoreCase);
                    if (numberCompare != 0) return numberCompare;
                    var zeroCompare = (leftDigits - leftStart).CompareTo(rightDigits - rightStart);
                    if (zeroCompare != 0) return zeroCompare;
                    continue;
                }

                var charCompare = char.ToUpperInvariant(left[i]).CompareTo(char.ToUpperInvariant(right[j]));
                if (charCompare != 0) return charCompare;
                i++;
                j++;
            }

            return (left.Length - i).CompareTo(right.Length - j);
        }
    }
}
