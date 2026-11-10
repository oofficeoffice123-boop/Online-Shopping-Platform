using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopEase.Application.Search;

namespace ShopEase.API.Controllers;

[ApiController]
[Route("api/categories")]
[AllowAnonymous]
public sealed class CategoriesController(IProductBrowsingService browsing) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryListItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await browsing.GetCategoriesAsync(cancellationToken));
}
