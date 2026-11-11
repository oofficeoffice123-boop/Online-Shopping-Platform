using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZSquare.API.Security;
using ZSquare.Domain;
using ZSquare.Infrastructure;

namespace ZSquare.API.Controllers;

[ApiController, Route("api/manage/reviews")]
public sealed class ManageReviewsController(AppDbContext db, ILogger<ManageReviewsController> logger) : ControllerBase
{
    [HttpGet, Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> List([FromQuery]int page=1,[FromQuery]int pageSize=20,int? productId=null,int? rating=null,bool? hidden=null,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);
        var query=db.Reviews.AsNoTracking().AsQueryable();
        if(productId.HasValue)query=query.Where(review=>review.ProductId==productId.Value);
        if(rating.HasValue)query=query.Where(review=>review.Rating==rating.Value);
        if(hidden.HasValue)query=query.Where(review=>review.IsHidden==hidden.Value);
        var total=await query.CountAsync(ct);
        var rows=await query.OrderByDescending(review=>review.CreatedAt).ThenByDescending(review=>review.ReviewId)
            .Skip((page-1)*pageSize).Take(pageSize)
            .Select(review=>new{review.ReviewId,review.ProductId,Product=review.Product.Name,review.Rating,review.Comment,review.CreatedAt,review.IsHidden,Reviewer=review.User.FullName,review.ModeratedAt,Moderator=review.ModeratedBy==null?null:review.ModeratedBy.FullName})
            .ToListAsync(ct);
        var items=rows.Select(row=>new ManageReviewDto(row.ReviewId,row.ProductId,row.Product,ReviewerName(row.Reviewer),row.Rating,row.Comment,row.CreatedAt,row.IsHidden,row.ModeratedAt,row.Moderator is null?null:ReviewerName(row.Moderator))).ToArray();
        return Ok(new{items,page,pageSize,totalCount=total,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpPatch("{id:int}/hidden"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> SetHidden(int id,SetReviewHiddenRequest request,[FromServices]IValidator<SetReviewHiddenRequest> validator,CancellationToken ct)
    {
        var result=await validator.ValidateAsync(request,ct);
        if(!result.IsValid)return ValidationProblem(new ValidationProblemDetails(result.Errors.GroupBy(error=>error.PropertyName).ToDictionary(group=>group.Key,group=>group.Select(error=>error.ErrorMessage).ToArray())));
        var review=await db.Reviews.SingleOrDefaultAsync(item=>item.ReviewId==id,ct);if(review is null)return NotFound();
        review.IsHidden=request.IsHidden;review.ModeratedByUserId=User.GetUserId();review.ModeratedAt=PakistanClock.Now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Management action review.moderate by {UserId} on review {ReviewId}; hidden={IsHidden}",User.GetUserId(),id,request.IsHidden);
        return Ok(new{id=review.ReviewId,review.IsHidden,review.ModeratedAt});
    }

    [HttpDelete("{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Delete(int id,CancellationToken ct)
    {var review=await db.Reviews.FindAsync([id],ct);if(review is null)return NotFound();db.Reviews.Remove(review);await db.SaveChangesAsync(ct);logger.LogInformation("Management action review.delete by {UserId} on review {ReviewId}",User.GetUserId(),id);return NoContent();}

    private static string ReviewerName(string fullName){var parts=fullName.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);return parts.Length<2?parts.FirstOrDefault()??"Customer":$"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";}
}

public sealed record SetReviewHiddenRequest(bool IsHidden);
public sealed record ManageReviewDto(int ReviewId,int ProductId,string ProductName,string ReviewerName,int Rating,string? Comment,DateTime CreatedAt,bool IsHidden,DateTime? ModeratedAt,string? ModeratedBy);
public sealed class SetReviewHiddenRequestValidator:AbstractValidator<SetReviewHiddenRequest>{public SetReviewHiddenRequestValidator(){RuleFor(request=>request.IsHidden).NotNull();}}
