using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZSquare.API.Security;
using ZSquare.Application.Commerce;

namespace ZSquare.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = "CustomerOnly")]
public sealed class OrdersController(ICustomerCommerceService commerce) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] IValidator<CheckoutRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return CartController.ValidationFailure(validation);
        var result = await commerce.CheckoutAsync(User.GetUserId(), request, idempotencyKey, cancellationToken);
        if (!result.Succeeded)
            return CartController.Failure(result.Error!);
        var checkout = result.Value!;
        return checkout.Replayed
            ? Ok(checkout.Order)
            : CreatedAtAction(nameof(Detail), new { id = checkout.Order.OrderId }, checkout.Order);
    }

    [HttpGet]
    public async Task<ActionResult<OrderPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default) =>
        Ok(await commerce.GetOrdersAsync(User.GetUserId(), page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken cancellationToken)
    {
        var order = await commerce.GetOrderAsync(User.GetUserId(), id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var result = await commerce.CancelOrderAsync(User.GetUserId(), id, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : CartController.Failure(result.Error!);
    }
}
