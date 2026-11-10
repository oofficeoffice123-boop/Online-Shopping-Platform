using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopEase.API.Security;
using ShopEase.Application.Commerce;

namespace ShopEase.API.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize(Policy = "CustomerOnly")]
public sealed class AddressesController(ICustomerCommerceService commerce) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddressDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await commerce.GetAddressesAsync(User.GetUserId(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Add(
        AddressRequest request,
        [FromServices] IValidator<AddressRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return CartController.ValidationFailure(validation);
        var address = await commerce.AddAddressAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), null, address);
    }

    [HttpPut("{addressId:int}")]
    public async Task<IActionResult> Update(
        int addressId,
        AddressRequest request,
        [FromServices] IValidator<AddressRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return CartController.ValidationFailure(validation);
        var result = await commerce.UpdateAddressAsync(User.GetUserId(), addressId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : CartController.Failure(result.Error!);
    }

    [HttpDelete("{addressId:int}")]
    public async Task<IActionResult> Delete(int addressId, CancellationToken cancellationToken)
    {
        var result = await commerce.DeleteAddressAsync(User.GetUserId(), addressId, cancellationToken);
        return result.Succeeded ? NoContent() : CartController.Failure(result.Error!);
    }
}
