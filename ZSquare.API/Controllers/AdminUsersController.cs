using System.Data;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZSquare.API.Security;
using ZSquare.Application;
using ZSquare.Application.Auth;
using ZSquare.Domain;
using ZSquare.Infrastructure;

namespace ZSquare.API.Controllers;

[ApiController, Route("api/admin/users"), Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersController(AppDbContext db, IUserPasswordHasher hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        string? search = null, string? role = null, bool? deleted = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(user => user.FullName.Contains(search) || user.Email.Contains(search));
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(user => user.Role.Name == role);
        if (deleted.HasValue) query = query.Where(user => user.IsDeleted == deleted.Value);

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(user => user.FullName).ThenBy(user => user.UserId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new AdminUserDto(user.UserId, user.FullName, user.Email, user.Phone,
                user.Role.Name, user.IsActive, user.IsDeleted, user.CreatedAt))
            .ToListAsync(ct);
        return Ok(new { items = users, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAdminUserRequest request,
        [FromServices] IValidator<CreateAdminUserRequest> validator, CancellationToken ct)
    {
        var errors = await Validate(validator, request, ct);
        if (errors is not null) return errors;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Email is already registered." });
        var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == request.Role, ct);
        if (role is null) return BadRequest(new ProblemDetails { Status = 400, Title = "Choose Customer, Manager, or Admin." });

        var user = new User
        {
            FullName = request.FullName.Trim(), Email = email, Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RoleId = role.RoleId, Role = role, IsActive = true, CreatedAt = PakistanClock.Now, CreatedBy = User.GetUserId()
        };
        user.PasswordHash = hasher.Hash(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        AddAudit("user.create", "User", user.UserId, new { user.Email, role = role.Name });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Created($"/api/admin/users/{user.UserId}", ToDto(user));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateAdminUserRequest request,
        [FromServices] IValidator<UpdateAdminUserRequest> validator, CancellationToken ct)
    {
        var errors = await Validate(validator, request, ct);
        if (errors is not null) return errors;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.Include(item => item.Role).SingleOrDefaultAsync(item => item.UserId == id && !item.IsDeleted, ct);
        if (user is null) return NotFound();
        if (!IsKnownRole(request.Role)) return BadRequest(new ProblemDetails { Status = 400, Title = "Choose Customer, Manager, or Admin." });
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(item => item.UserId != id && item.Email == email, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Email is already registered." });
        if (id == User.GetUserId() && (request.Role != Roles.Admin || !request.IsActive))
            return ConflictProblem("You cannot demote or disable your own Admin account.");
        if (!request.IsActive && user.IsActive && user.Role.Name == Roles.Admin && !await HasOtherActiveAdmin(id, ct))
            return ConflictProblem("The last active Admin cannot be disabled.");

        var role = await db.Roles.SingleAsync(item => item.Name == request.Role, ct);
        var before = new { user.FullName, user.Email, user.Phone, Role = user.Role.Name, user.IsActive };
        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.RoleId = role.RoleId;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = PakistanClock.Now;
        user.UpdatedBy = User.GetUserId();
        var passwordChanged = !string.IsNullOrWhiteSpace(request.Password);
        if (passwordChanged) user.PasswordHash = hasher.Hash(user, request.Password!);
        if (passwordChanged || !user.IsActive || before.Role != role.Name || before.Email != email) await RevokeTokens(id, ct);
        AddAudit("user.update", "User", id, new { before, after = new { user.FullName, user.Email, user.Phone, Role = role.Name, user.IsActive } });
        if (passwordChanged) AddAudit("user.password_change", "User", id, new { user.Email });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(ToDto(user));
    }

    [HttpPatch("{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, ChangeAdminPasswordRequest request,
        [FromServices] IValidator<ChangeAdminPasswordRequest> validator, CancellationToken ct)
    {
        var errors = await Validate(validator, request, ct);
        if (errors is not null) return errors;
        var user = await db.Users.SingleOrDefaultAsync(item => item.UserId == id && !item.IsDeleted, ct);
        if (user is null) return NotFound();
        user.PasswordHash = hasher.Hash(user, request.Password);
        user.UpdatedAt = PakistanClock.Now;
        user.UpdatedBy = User.GetUserId();
        await RevokeTokens(id, ct);
        AddAudit("user.password_change", "User", id, new { user.Email });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/deleted")]
    public async Task<IActionResult> SetDeleted(int id, SetUserDeletedRequest request, CancellationToken ct)
    {
        var user = await db.Users.Include(item => item.Role).SingleOrDefaultAsync(item => item.UserId == id, ct);
        if (user is null) return NotFound();
        if (id == User.GetUserId() && request.IsDeleted) return ConflictProblem("You cannot delete your own Admin account.");
        if (request.IsDeleted && user.Role.Name == Roles.Admin &&
            !string.Equals(request.Confirmation?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["confirmation"] = [$"Enter {user.Email} to confirm deleting this Admin account."] }));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (request.IsDeleted && !user.IsDeleted)
        {
            if (user.Role.Name == Roles.Admin && !await HasOtherActiveAdmin(id, ct))
                return ConflictProblem("The last active Admin cannot be deleted.");
            user.IsDeleted = true;
            user.IsActive = false;
            user.UpdatedAt = PakistanClock.Now;
            user.UpdatedBy = User.GetUserId();
            await RevokeTokens(id, ct);
        }
        else if (!request.IsDeleted && user.IsDeleted)
        {
            user.IsDeleted = false;
            user.IsActive = true;
            user.UpdatedAt = PakistanClock.Now;
            user.UpdatedBy = User.GetUserId();
        }
        AddAudit(request.IsDeleted ? "user.soft_delete" : "user.restore", "User", id,
            new { user.Email, user.Role.Name, user.IsActive, user.IsDeleted });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(ToDto(user));
    }

    private async Task<IActionResult?> Validate<T>(IValidator<T> validator, T value, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(value, ct);
        if (result.IsValid) return null;
        return BadRequest(new ValidationProblemDetails(result.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())));
    }

    private Task<bool> HasOtherActiveAdmin(int except, CancellationToken ct) =>
        db.Users.AnyAsync(user => user.UserId != except && user.IsActive && !user.IsDeleted && user.Role.Name == Roles.Admin, ct);

    private async Task RevokeTokens(int id, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(token => token.UserId == id && token.RevokedAt == null).ToListAsync(ct);
        foreach (var token in tokens) token.RevokedAt = PakistanClock.Now;
    }

    private void AddAudit(string action, string entityType, int? id, object details) => db.AuditLogs.Add(new AuditLog
    {
        ActorUserId = User.GetUserId(), Action = action, EntityType = entityType,
        EntityId = id?.ToString(), DetailsJson = JsonSerializer.Serialize(details), CreatedAt = PakistanClock.Now
    });

    private static bool IsKnownRole(string role) => role is Roles.Customer or Roles.Manager or Roles.Admin;
    private static AdminUserDto ToDto(User user) => new(user.UserId, user.FullName, user.Email, user.Phone, user.Role.Name, user.IsActive, user.IsDeleted, user.CreatedAt);
    private ConflictObjectResult ConflictProblem(string detail) => Conflict(new ProblemDetails { Status = 409, Title = "Conflict", Detail = detail });
}

public sealed record AdminUserDto(int UserId, string FullName, string Email, string? Phone, string Role, bool IsActive, bool IsDeleted, DateTime CreatedAt);
public sealed record CreateAdminUserRequest(string FullName, string Email, string Role, string Password, string? Phone);
public sealed record UpdateAdminUserRequest(string FullName, string Email, string Role, bool IsActive, string? Phone, string? Password = null);
public sealed record ChangeAdminPasswordRequest(string Password);
public sealed record SetUserDeletedRequest(bool IsDeleted, string? Confirmation = null);

public sealed class CreateAdminUserRequestValidator : AbstractValidator<CreateAdminUserRequest>
{
    public CreateAdminUserRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Role).Must(role => role is Roles.Customer or Roles.Manager or Roles.Admin).WithMessage("Role must be Customer, Manager, or Admin.");
        RuleFor(request => request.Password).NotEmpty().MinimumLength(8).Matches("(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)")
            .WithMessage("Password must contain an uppercase letter, a lowercase letter, and a number.");
        RuleFor(request => request.Phone).MaximumLength(30);
    }
}

public sealed class UpdateAdminUserRequestValidator : AbstractValidator<UpdateAdminUserRequest>
{
    public UpdateAdminUserRequestValidator()
    {
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Role).Must(role => role is Roles.Customer or Roles.Manager or Roles.Admin).WithMessage("Role must be Customer, Manager, or Admin.");
        RuleFor(request => request.Phone).MaximumLength(30);
        RuleFor(request => request.Password).MinimumLength(8).Matches("(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)")
            .WithMessage("Password must contain an uppercase letter, a lowercase letter, and a number.")
            .When(request => !string.IsNullOrWhiteSpace(request.Password));
    }
}

public sealed class ChangeAdminPasswordRequestValidator : AbstractValidator<ChangeAdminPasswordRequest>
{
    public ChangeAdminPasswordRequestValidator()
    {
        RuleFor(request => request.Password).NotEmpty().MinimumLength(8).Matches("(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)")
            .WithMessage("Password must contain an uppercase letter, a lowercase letter, and a number.");
    }
}
