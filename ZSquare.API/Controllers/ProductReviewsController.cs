using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZSquare.API.Security;
using ZSquare.Domain;
using ZSquare.Infrastructure;
namespace ZSquare.API.Controllers;

[ApiController]
public sealed class ProductReviewsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/products/{productId:int}/reviews"), AllowAnonymous]
    public async Task<IActionResult> List(int productId, [FromQuery]int page = 1, [FromQuery]int pageSize = 10, CancellationToken ct = default)
    {
        if (!await db.Products.AnyAsync(product => product.ProductId == productId && product.IsActive, ct)) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Reviews.AsNoTracking().Where(review => review.ProductId == productId && !review.IsHidden);
        var summary = await query.GroupBy(_ => 1).Select(group => new
        {
            Average = group.Average(review => (decimal)review.Rating),
            Count = group.Count()
        }).SingleOrDefaultAsync(ct);
        var stars = await query.GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() }).ToListAsync(ct);
        var counts = Enumerable.Range(1, 5).ToDictionary(rating => rating, rating => stars.SingleOrDefault(x => x.Rating == rating)?.Count ?? 0);
        var rows = await query.OrderByDescending(review => review.CreatedAt).ThenByDescending(review => review.ReviewId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(review => new { review.ReviewId, review.User.FullName, review.Rating, review.Comment, review.CreatedAt })
            .ToListAsync(ct);
        var items = rows.Select(row => new PublicReviewDto(row.ReviewId, ReviewerName(row.FullName), row.Rating, row.Comment, row.CreatedAt)).ToArray();
        var totalCount = summary?.Count ?? 0;
        return Ok(new ReviewPageDto(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize),
            new ReviewSummaryDto(summary is null ? null : decimal.Round(summary.Average, 1), totalCount, counts)));
    }

    [HttpGet("api/products/{productId:int}/reviews/eligibility"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Eligibility(int productId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(product => product.ProductId == productId && product.IsActive, ct)) return NotFound();
        var userId = User.GetUserId();
        var existing = await db.Reviews.AsNoTracking().Where(review => review.ProductId == productId && review.UserId == userId)
            .Select(review => new ExistingReviewDto(review.ReviewId, review.Rating, review.Comment, review.CreatedAt, review.IsHidden))
            .SingleOrDefaultAsync(ct);
        var purchased = await HasDeliveredPurchase(productId, userId, ct);
        return Ok(new { canReview = purchased && existing is null, hasDeliveredPurchase = purchased, existingReview = existing });
    }

    [HttpPost("api/products/{productId:int}/reviews"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Create(int productId, ReviewWriteRequest request, [FromServices]IValidator<ReviewWriteRequest> validator, CancellationToken ct)
    {
        request = request with { Comment = request.Comment?.Trim() };
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.Errors
            .GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())));
        if (!await db.Products.AnyAsync(product => product.ProductId == productId && product.IsActive, ct)) return NotFound();
        var userId = User.GetUserId();
        if (!await HasDeliveredPurchase(productId, userId, ct)) return ConflictProblem("A delivered purchase of this product is required to review it.");
        if (await db.Reviews.AnyAsync(review => review.ProductId == productId && review.UserId == userId, ct)) return ConflictProblem("You have already reviewed this product.");
        var review = new Review { ProductId = productId, UserId = userId, Rating = request.Rating, Comment = NormalizeComment(request.Comment), CreatedAt = PakistanClock.Now };
        db.Reviews.Add(review);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return ConflictProblem("A review already exists for this product."); }
        var name = await db.Users.Where(user => user.UserId == userId).Select(user => user.FullName).SingleAsync(ct);
        return Created($"/api/products/{productId}/reviews", new PublicReviewDto(review.ReviewId, ReviewerName(name), review.Rating, review.Comment, review.CreatedAt));
    }

    [HttpPut("api/reviews/{reviewId:int}"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Update(int reviewId, ReviewWriteRequest request, [FromServices]IValidator<ReviewWriteRequest> validator, CancellationToken ct)
    {
        request = request with { Comment = request.Comment?.Trim() };
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(new ValidationProblemDetails(validation.Errors
            .GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())));
        var review = await db.Reviews.Include(item => item.User).SingleOrDefaultAsync(item => item.ReviewId == reviewId && item.UserId == User.GetUserId(), ct);
        if (review is null) return NotFound();
        review.Rating = request.Rating; review.Comment = NormalizeComment(request.Comment);
        await db.SaveChangesAsync(ct);
        return Ok(new PublicReviewDto(review.ReviewId, ReviewerName(review.User.FullName), review.Rating, review.Comment, review.CreatedAt));
    }

    [HttpDelete("api/reviews/{reviewId:int}"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Delete(int reviewId, CancellationToken ct)
    {
        var review = await db.Reviews.SingleOrDefaultAsync(item => item.ReviewId == reviewId && item.UserId == User.GetUserId(), ct);
        if (review is null) return NotFound();
        db.Reviews.Remove(review); await db.SaveChangesAsync(ct); return NoContent();
    }

    private Task<bool> HasDeliveredPurchase(int productId, int userId, CancellationToken ct) => db.Orders.AnyAsync(order =>
        order.UserId == userId && order.Status.Name == "Delivered" && order.Items.Any(item => item.Variant.ProductId == productId), ct);
    private static string ReviewerName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 ? parts.FirstOrDefault() ?? "Customer" : $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }
    private static string? NormalizeComment(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    private ConflictObjectResult ConflictProblem(string detail) => Conflict(new ProblemDetails { Status = 409, Title = "Review conflict", Detail = detail });
}

public sealed record ReviewWriteRequest(int Rating, string? Comment);
public sealed record PublicReviewDto(int ReviewId, string ReviewerName, int Rating, string? Comment, DateTime CreatedAt);
public sealed record ExistingReviewDto(int ReviewId, int Rating, string? Comment, DateTime CreatedAt, bool IsHidden);
public sealed record ReviewSummaryDto(decimal? AverageRating, int TotalCount, IReadOnlyDictionary<int, int> CountsByStar);
public sealed record ReviewPageDto(IReadOnlyList<PublicReviewDto> Items, int Page, int PageSize, int TotalCount, int TotalPages, ReviewSummaryDto Summary);

public sealed class ReviewWriteRequestValidator : AbstractValidator<ReviewWriteRequest>
{
    public ReviewWriteRequestValidator()
    {
        RuleFor(request => request.Rating).InclusiveBetween(1, 5);
        RuleFor(request => request.Comment).Must(comment => comment is null || comment.Trim().Length <= 1000)
            .WithMessage("Comment must be at most 1000 characters.");
        RuleFor(request => request.Comment).Must(comment => comment is null || !string.IsNullOrWhiteSpace(comment))
            .WithMessage("Comment cannot contain only whitespace.");
    }
}
