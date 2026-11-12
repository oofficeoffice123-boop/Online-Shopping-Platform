using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZSquare.Infrastructure;

namespace ZSquare.API.Controllers;

[ApiController, Route("api/admin/audit-logs"), Authorize(Policy = "AdminOnly")]
public sealed class AdminAuditLogsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        string? action = null, string? entityType = null, string? search = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(log => log.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(log => log.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(log => log.Action.Contains(search) || log.EntityType.Contains(search) ||
                                       (log.EntityId != null && log.EntityId.Contains(search)) ||
                                       (log.Actor != null && (log.Actor.FullName.Contains(search) || log.Actor.Email.Contains(search))));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.AuditLogId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new
            {
                log.AuditLogId, log.Action, log.EntityType, log.EntityId, log.DetailsJson, log.CreatedAt,
                ActorUserId = log.ActorUserId,
                ActorName = log.Actor == null ? "System" : log.Actor.FullName,
                ActorEmail = log.Actor == null ? null : log.Actor.Email
            }).ToListAsync(ct);
        return Ok(new { items, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }
}
