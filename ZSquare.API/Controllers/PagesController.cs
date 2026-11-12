using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZSquare.Infrastructure;

namespace ZSquare.API.Controllers;

[ApiController, Route("api/pages"), Authorize]
public sealed class PagesController(AppDbContext db) : ControllerBase
{
    [HttpGet("my-pages")]
    public async Task<IActionResult> MyPages(CancellationToken ct)
    {
        var role = User.FindFirst("roles")?.Value;
        if (string.IsNullOrWhiteSpace(role)) return Forbid();

        var pages = await db.RolePages.AsNoTracking()
            .Where(item => item.Role.Name == role)
            .OrderBy(item => item.Page.SortOrder)
            .Select(item => new PageAccessDto(item.Page.PageId, item.Page.Name, item.Page.Route, item.Page.SortOrder))
            .ToListAsync(ct);
        return Ok(pages);
    }
}

public sealed record PageAccessDto(int PageId, string Name, string Route, int SortOrder);
