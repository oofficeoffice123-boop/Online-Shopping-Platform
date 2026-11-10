using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopEase.API.Security;
using ShopEase.Application.Commerce;

namespace ShopEase.API.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = "CustomerOnly")]
public sealed class CartController(ICustomerCommerceService commerce) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken cancellationToken) =>
        Ok(await commerce.GetCartAsync(User.GetUserId(), cancellationToken));

    [HttpPost("items")]
    public async Task<IActionResult> Add(
        AddCartItemRequest request,
        [FromServices] IValidator<AddCartItemRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure(validation);
        var result = await commerce.AddCartItemAsync(User.GetUserId(), request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpPut("items/{cartItemId:int}")]
    public async Task<IActionResult> SetQuantity(
        int cartItemId,
        SetCartItemQuantityRequest request,
        [FromServices] IValidator<SetCartItemQuantityRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailure(validation);
        var result = await commerce.SetCartItemQuantityAsync(User.GetUserId(), cartItemId, request.Quantity, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Failure(result.Error!);
    }

    [HttpDelete("items/{cartItemId:int}")]
    public async Task<IActionResult> Remove(int cartItemId, CancellationToken cancellationToken)
    {
        var result = await commerce.RemoveCartItemAsync(User.GetUserId(), cartItemId, cancellationToken);
        return result.Succeeded ? NoContent() : Failure(result.Error!);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await commerce.ClearCartAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    internal static IActionResult Failure(CommerceError error)
    {
        if (error.Errors.Count > 0)
            return new ObjectResult(new ValidationProblemDetails(error.Errors.ToDictionary())
            {
                Status = error.StatusCode,
                Title = error.Title
            }) { StatusCode = error.StatusCode };
        return new ObjectResult(new ProblemDetails { Status = error.StatusCode, Title = error.Title })
        {
            StatusCode = error.StatusCode
        };
    }

    internal static IActionResult ValidationFailure(ValidationResult validation) =>
        new BadRequestObjectResult(new ValidationProblemDetails(validation.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray())));
}
