using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopEase.API.Security;
using ShopEase.Application;
using ShopEase.Application.Auth;
using ShopEase.Domain;
using ShopEase.Infrastructure;

namespace ShopEase.API.Controllers;

[ApiController,Route("api/admin/users"),Authorize(Policy="AdminOnly")]
public sealed class AdminUsersController(AppDbContext db,IUserPasswordHasher hasher,ILogger<AdminUsersController> logger):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]int page=1,[FromQuery]int pageSize=20,string? search=null,string? role=null,bool? active=null,CancellationToken ct=default)
    {page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var q=db.Users.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(search))q=q.Where(u=>u.FullName.Contains(search)||u.Email.Contains(search));if(!string.IsNullOrWhiteSpace(role))q=q.Where(u=>u.Role.Name==role);if(active.HasValue)q=q.Where(u=>u.IsActive==active.Value);var total=await q.CountAsync(ct);var users=await q.OrderBy(u=>u.FullName).Skip((page-1)*pageSize).Take(pageSize).Select(u=>new{u.UserId,u.FullName,u.Email,u.IsActive,u.MustChangePassword,role=u.Role.Name,u.CreatedAt}).ToListAsync(ct);return Ok(new{items=users,page,pageSize,totalCount=total});}

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffRequest request,[FromServices]IValidator<CreateStaffRequest> validator,CancellationToken ct)
    {var valid=await validator.ValidateAsync(request,ct);if(!valid.IsValid)return BadRequest(new ValidationProblemDetails(valid.Errors.GroupBy(e=>e.PropertyName).ToDictionary(g=>g.Key,g=>g.Select(x=>x.ErrorMessage).ToArray())));var roleName=request.Role.Trim();if(roleName is not (Roles.Manager or Roles.Admin))return BadRequest(new ProblemDetails{Status=400,Title="Staff role must be Manager or Admin."});if(await db.Users.AnyAsync(u=>u.Email==request.Email.Trim().ToLowerInvariant(),ct))return Conflict(new ProblemDetails{Status=409,Title="Email is already registered."});var role=await db.Roles.SingleOrDefaultAsync(r=>r.Name==roleName,ct);if(role==null)return Conflict(new ProblemDetails{Status=409,Title=$"Role '{roleName}' is not configured."});var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))+"aA1!";var user=new User{FullName=request.FullName.Trim(),Email=request.Email.Trim().ToLowerInvariant(),RoleId=role.RoleId,Role=role,IsActive=true,MustChangePassword=true,CreatedAt=DateTime.UtcNow};user.PasswordHash=hasher.Hash(user,password);db.Users.Add(user);await db.SaveChangesAsync(ct);logger.LogInformation("Management action user.create by {UserId} on {EntityId}",User.GetUserId(),user.UserId);return Created($"/api/admin/users/{user.UserId}",new{user.UserId,user.FullName,user.Email,role=roleName,temporaryPassword=password,user.MustChangePassword});}

    [HttpPatch("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id,ChangeUserRoleRequest request,[FromServices]IValidator<ChangeUserRoleRequest> validator,CancellationToken ct)
    {var valid=await validator.ValidateAsync(request,ct);if(!valid.IsValid)return BadRequest(new ValidationProblemDetails(valid.Errors.GroupBy(e=>e.PropertyName).ToDictionary(g=>g.Key,g=>g.Select(x=>x.ErrorMessage).ToArray())));if(id==User.GetUserId())return ConflictProblem("An Admin cannot change their own role.");var user=await db.Users.Include(u=>u.Role).SingleOrDefaultAsync(u=>u.UserId==id,ct);if(user==null)return NotFound();var role=await db.Roles.SingleOrDefaultAsync(r=>r.Name==request.Role,ct);if(role==null)return BadRequest(new ProblemDetails{Status=400,Title="Unknown role."});if(user.IsActive&&user.Role.Name==Roles.Admin&&role.Name!=Roles.Admin&&!await HasOtherActiveAdmin(id,ct))return ConflictProblem("The last active Admin cannot be demoted.");user.RoleId=role.RoleId;user.Role=role;await RevokeTokens(id,ct);await db.SaveChangesAsync(ct);Log("user.role",id);return Ok(new{user.UserId,role=role.Name});}

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id,SetUserActiveRequest request,[FromServices]IValidator<SetUserActiveRequest> validator,CancellationToken ct)
    {var valid=await validator.ValidateAsync(request,ct);if(!valid.IsValid)return BadRequest(new ValidationProblemDetails(valid.Errors.GroupBy(e=>e.PropertyName).ToDictionary(g=>g.Key,g=>g.Select(x=>x.ErrorMessage).ToArray())));if(id==User.GetUserId()&&!request.IsActive)return ConflictProblem("An Admin cannot disable themselves.");var user=await db.Users.Include(u=>u.Role).SingleOrDefaultAsync(u=>u.UserId==id,ct);if(user==null)return NotFound();if(!request.IsActive&&user.IsActive&&user.Role.Name==Roles.Admin&&!await HasOtherActiveAdmin(id,ct))return ConflictProblem("The last active Admin cannot be disabled.");user.IsActive=request.IsActive;if(!request.IsActive)await RevokeTokens(id,ct);await db.SaveChangesAsync(ct);Log("user.active",id);return Ok(new{user.UserId,user.IsActive});}

    private Task<bool> HasOtherActiveAdmin(int except,CancellationToken ct)=>db.Users.AnyAsync(u=>u.UserId!=except&&u.IsActive&&u.Role.Name==Roles.Admin,ct);
    private async Task RevokeTokens(int id,CancellationToken ct){var tokens=await db.RefreshTokens.Where(t=>t.UserId==id&&t.RevokedAt==null).ToListAsync(ct);foreach(var t in tokens)t.RevokedAt=DateTime.UtcNow;}
    private void Log(string action,int id)=>logger.LogInformation("Management action {Action} by {UserId} on {EntityId}",action,User.GetUserId(),id);
    private ConflictObjectResult ConflictProblem(string detail)=>Conflict(new ProblemDetails{Status=409,Title="Conflict",Detail=detail});
}

public sealed record CreateStaffRequest(string FullName,string Email,string Role);
public sealed record ChangeUserRoleRequest(string Role);
public sealed record SetUserActiveRequest(bool IsActive);
public sealed class CreateStaffRequestValidator:AbstractValidator<CreateStaffRequest>{public CreateStaffRequestValidator(){RuleFor(x=>x.FullName).NotEmpty().MaximumLength(200);RuleFor(x=>x.Email).NotEmpty().EmailAddress().MaximumLength(256);RuleFor(x=>x.Role).Must(x=>x is Roles.Manager or Roles.Admin).WithMessage("Role must be Manager or Admin.");}}
public sealed class ChangeUserRoleRequestValidator:AbstractValidator<ChangeUserRoleRequest>{public ChangeUserRoleRequestValidator(){RuleFor(x=>x.Role).NotEmpty().Must(x=>x is Roles.Customer or Roles.Manager or Roles.Admin).WithMessage("Role must be Customer, Manager or Admin.");}}
public sealed class SetUserActiveRequestValidator:AbstractValidator<SetUserActiveRequest>{public SetUserActiveRequestValidator(){RuleFor(x=>x.IsActive).NotNull();}}
