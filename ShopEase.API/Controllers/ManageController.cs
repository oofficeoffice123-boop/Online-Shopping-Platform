using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopEase.API.Security;
using ShopEase.Domain;
using ShopEase.Infrastructure;
using ShopEase.Infrastructure.Commerce;

namespace ShopEase.API.Controllers;

[ApiController]
[Route("api/manage")]
public sealed class ManageController(AppDbContext db, IWebHostEnvironment env, ILogger<ManageController> log) : ControllerBase
{
    [HttpGet("products"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Products([FromQuery] int page = 1, [FromQuery] int pageSize = 20, string? search = null,
        int? categoryId = null, bool? active = null, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search) || p.ProductCode.Contains(search));
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);
        if (active.HasValue) q = q.Where(p => p.IsActive == active.Value);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.ProductId, p.ProductCode, p.Name, p.Description, p.CategoryId, Category = p.Category.Name,
                p.BasePrice, p.IsActive, p.Images }).ToListAsync(ct);
        return Ok(new { items = rows.Select(p => new { p.ProductId, p.ProductCode, p.Name, p.Description, p.CategoryId,
            p.Category, p.BasePrice, p.IsActive, images = p.Images.OrderBy(i => i.SortOrder).Select(i => new { i.ImageId, i.Url, i.IsPrimary, i.SortOrder }) }), page, pageSize, totalCount = total });
    }

    [HttpPost("products"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateProduct(ProductRequest r, [FromServices] IValidator<ProductRequest> validator, CancellationToken ct)
    {
        var errors = await Validate(validator, r, ct); if (errors != null) return errors;
        if (await db.Products.AnyAsync(p => p.ProductCode == r.ProductCode.Trim(), ct)) return ConflictProblem("ProductCode is already in use.");
        if (!await db.Categories.AnyAsync(c => c.CategoryId == r.CategoryId, ct)) return ValidationProblem(new Dictionary<string,string[]> { ["categoryId"] = ["Category does not exist."] });
        var p = new Product { ProductCode = r.ProductCode.Trim(), Name = r.Name.Trim(), Description = r.Description?.Trim(), CategoryId = r.CategoryId, BasePrice = r.BasePrice };
        db.Products.Add(p); await db.SaveChangesAsync(ct); Log("product.create", p.ProductId);
        return Created($"/api/manage/products/{p.ProductId}", ProductDto(p));
    }

    [HttpPut("products/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateProduct(int id, ProductRequest r, [FromServices] IValidator<ProductRequest> validator, CancellationToken ct)
    {
        var errors = await Validate(validator, r, ct); if (errors != null) return errors;
        var p = await db.Products.FindAsync([id], ct); if (p == null) return NotFound();
        if (await db.Products.AnyAsync(x => x.ProductId != id && x.ProductCode == r.ProductCode.Trim(), ct)) return ConflictProblem("ProductCode is already in use.");
        if (!await db.Categories.AnyAsync(c => c.CategoryId == r.CategoryId, ct)) return ValidationProblem(new Dictionary<string,string[]> { ["categoryId"] = ["Category does not exist."] });
        p.ProductCode = r.ProductCode.Trim(); p.Name = r.Name.Trim(); p.Description = r.Description?.Trim(); p.CategoryId = r.CategoryId; p.BasePrice = r.BasePrice;
        await db.SaveChangesAsync(ct); Log("product.update", id); return Ok(ProductDto(p));
    }

    [HttpPatch("products/{id:int}/active"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> SetProductActive(int id, ActiveRequest r, [FromServices] IValidator<ActiveRequest> validator, CancellationToken ct)
    { var errors = await Validate(validator, r, ct); if(errors!=null)return errors; var p=await db.Products.FindAsync([id],ct); if(p==null)return NotFound(); p.IsActive=r.IsActive; await db.SaveChangesAsync(ct); Log("product.active",id); return Ok(ProductDto(p)); }

    [HttpDelete("products/{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var p = await db.Products.Include(x=>x.Images).Include(x=>x.Variants).SingleOrDefaultAsync(x=>x.ProductId==id,ct); if(p==null)return NotFound();
        if(await db.OrderItems.AnyAsync(i=>i.Variant.ProductId==id,ct)) return ConflictProblem("This product appears in an order. Deactivate it instead.");
        var files=p.Images.Select(i=>PhysicalImagePath(i.Url)).Where(x=>x!=null).ToArray();
        db.Products.Remove(p); await db.SaveChangesAsync(ct); foreach(var f in files) if(System.IO.File.Exists(f!)) System.IO.File.Delete(f!);
        Log("product.delete",id); return NoContent();
    }

    [HttpPost("products/{id:int}/variants"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateVariant(int id, VariantCreateRequest r, [FromServices] IValidator<VariantCreateRequest> validator, CancellationToken ct)
    {
        var errors=await Validate(validator,r,ct); if(errors!=null)return errors;
        var p=await db.Products.FindAsync([id],ct); if(p==null)return NotFound();
        var size=await db.Sizes.FindAsync([r.SizeId],ct); var color=await db.Colors.FindAsync([r.ColorId],ct);
        if(size==null||color==null)return ValidationProblem(new Dictionary<string,string[]> { ["sizeId/colorId"]=["Size and color must exist."] });
        var sku=string.IsNullOrWhiteSpace(r.Sku)?$"{p.ProductCode}-{size.Label}-{color.Name}":r.Sku.Trim();
        if(await db.ProductVariants.AnyAsync(v=>v.ProductId==id&&v.SizeId==r.SizeId&&v.ColorId==r.ColorId,ct))return ConflictProblem("This product already has that size and color.");
        if(await db.ProductVariants.AnyAsync(v=>v.SKU==sku,ct))return ConflictProblem("SKU is already in use.");
        var v=new ProductVariant{ProductId=id,SizeId=r.SizeId,ColorId=r.ColorId,SKU=sku,PriceOverride=r.PriceOverride,StockQty=r.StockQty}; db.ProductVariants.Add(v); await db.SaveChangesAsync(ct); Log("variant.create",v.VariantId); return Created($"/api/manage/variants/{v.VariantId}",VariantDto(v));
    }

    [HttpGet("products/{id:int}/variants"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Variants(int id,CancellationToken ct){if(!await db.Products.AnyAsync(p=>p.ProductId==id,ct))return NotFound();return Ok(await db.ProductVariants.AsNoTracking().Where(v=>v.ProductId==id).OrderBy(v=>v.SKU).Select(v=>new{v.VariantId,v.ProductId,v.SizeId,v.ColorId,v.SKU,v.PriceOverride,v.StockQty,rowVersion=Convert.ToBase64String(v.RowVersion),size=v.Size.Label,color=v.Color.Name}).ToListAsync(ct));}

    [HttpPut("variants/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateVariant(int id, VariantUpdateRequest r, [FromServices] IValidator<VariantUpdateRequest> validator, CancellationToken ct)
    { var errors=await Validate(validator,r,ct);if(errors!=null)return errors;var v=await db.ProductVariants.FindAsync([id],ct);if(v==null)return NotFound();if(!TrySetVersion(v,r.RowVersion))return ConflictProblem("Invalid or stale variant RowVersion. Refresh and retry.");v.PriceOverride=r.PriceOverride;v.StockQty=r.StockQty;try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return ConflictProblem("Variant changed concurrently. Refresh and retry.");}Log("variant.update",id);return Ok(VariantDto(v)); }

    [HttpDelete("variants/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> DeleteVariant(int id,CancellationToken ct){var v=await db.ProductVariants.FindAsync([id],ct);if(v==null)return NotFound();if(await db.OrderItems.AnyAsync(i=>i.VariantId==id,ct))return ConflictProblem("This variant appears in an order. Set its stock to 0 instead.");db.ProductVariants.Remove(v);await db.SaveChangesAsync(ct);Log("variant.delete",id);return NoContent();}

    [HttpPatch("variants/{id:int}/stock"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Stock(int id, StockRequest r, [FromServices] IValidator<StockRequest> validator, CancellationToken ct)
    { var errors=await Validate(validator,r,ct);if(errors!=null)return errors;var v=await db.ProductVariants.FindAsync([id],ct);if(v==null)return NotFound();if(!TrySetVersion(v,r.RowVersion))return ConflictProblem("Invalid or stale variant RowVersion. Refresh and retry.");var next=r.Set??(long)v.StockQty+r.Delta!.Value;if(next<0||next>int.MaxValue)return ConflictProblem("Stock adjustment would result in an invalid quantity.");v.StockQty=(int)next;try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return ConflictProblem("Stock changed concurrently. Refresh and retry.");}Log("variant.stock",id);return Ok(VariantDto(v)); }

    [HttpGet("orders"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Orders([FromQuery]int page=1,[FromQuery]int pageSize=20,string? status=null,DateTime? from=null,DateTime? to=null,int? orderId=null,string? email=null,CancellationToken ct=default)
    { page=Math.Max(page,1);pageSize=Math.Clamp(pageSize,1,100);var q=db.Orders.AsNoTracking().AsQueryable();if(!string.IsNullOrWhiteSpace(status))q=q.Where(o=>o.Status.Name==status);if(from.HasValue)q=q.Where(o=>o.OrderDate>=from);if(to.HasValue)q=q.Where(o=>o.OrderDate<to.Value.AddDays(1));if(orderId.HasValue)q=q.Where(o=>o.OrderId==orderId);if(!string.IsNullOrWhiteSpace(email))q=q.Where(o=>o.User.Email.Contains(email));var total=await q.CountAsync(ct);var items=await q.OrderByDescending(o=>o.OrderDate).Skip((page-1)*pageSize).Take(pageSize).Select(o=>new{ o.OrderId,o.OrderDate,o.Status.Name,o.TotalAmount,Customer=o.User.Email,Items=o.Items.Count}).ToListAsync(ct);return Ok(new{items,page,pageSize,totalCount=total}); }

    [HttpGet("orders/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Order(int id,CancellationToken ct)
    { var o=await db.Orders.AsNoTracking().Include(x=>x.Status).Include(x=>x.User).Include(x=>x.Payments).Include(x=>x.Items).ThenInclude(x=>x.Variant).ThenInclude(x=>x.Product).SingleOrDefaultAsync(x=>x.OrderId==id,ct);if(o==null)return NotFound();return Ok(new{o.OrderId,o.OrderDate,status=o.Status.Name,o.TotalAmount,customer=new{o.User.UserId,o.User.FullName,o.User.Email},o.ShipName,o.ShipLine1,o.ShipLine2,o.ShipCity,o.ShipPostalCode,o.ShipCountry,items=o.Items.Select(i=>new{i.Variant.Product.Name,i.Quantity,i.UnitPrice,lineTotal=i.Quantity*i.UnitPrice}),payments=o.Payments.Select(p=>new{p.Method,p.Status,p.Amount,p.TransactionRef,p.PaidAt})}); }

    [HttpPatch("orders/{id:int}/status"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> ChangeStatus(int id, StatusRequest r, [FromServices] IValidator<StatusRequest> validator,CancellationToken ct)
    {
        var errors=await Validate(validator,r,ct);if(errors!=null)return errors;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var o=await db.Orders.Include(x=>x.Status).Include(x=>x.Items).ThenInclude(x=>x.Variant).Include(x=>x.Payments).SingleOrDefaultAsync(x=>x.OrderId==id,ct);if(o==null)return NotFound();
        var allowed=o.Status.Name switch{"Pending"=>new[]{"Paid","Cancelled"},"Paid"=>new[]{"Shipped","Cancelled"},"Shipped"=>new[]{"Delivered"},_=>Array.Empty<string>()};
        if(!allowed.Contains(r.Status,StringComparer.OrdinalIgnoreCase))return Conflict(new ProblemDetails{Status=409,Title="Invalid order status transition.",Detail=$"Allowed next statuses: {(allowed.Length==0?"none":string.Join(", ",allowed))}."});
        var target=await db.OrderStatuses.SingleOrDefaultAsync(x=>x.Name==r.Status,ct);if(target==null)return ValidationProblem(new Dictionary<string,string[]>{{"status",["Unknown status."]}});
        var from=o.Status.Name;o.Status=target;o.StatusId=target.StatusId;
        if(target.Name=="Cancelled")CustomerCommerceService.ApplyCancellation(o,target);
        if(target.Name=="Delivered")foreach(var p in o.Payments.Where(p=>p.Method=="CashOnDelivery")){p.Status="Succeeded";p.PaidAt=DateTime.UtcNow;}
        db.OrderStatusHistory.Add(new OrderStatusHistory{OrderId=id,FromStatus=from,ToStatus=target.Name,ChangedByUserId=User.GetUserId(),ChangedAt=DateTime.UtcNow});
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){return ConflictProblem("Order or inventory changed concurrently. Refresh and retry.");}
        Log("order.status",id);return Ok(new{id,status=target.Name,allowedNextStatuses=Allowed(target.Name)});
    }

    [HttpGet("dashboard"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {var today=DateTime.UtcNow.Date;var byStatus=await db.Orders.AsNoTracking().GroupBy(o=>o.Status.Name).Select(g=>new{status=g.Key,count=g.Count()}).ToListAsync(ct);var recent=await db.Orders.AsNoTracking().OrderByDescending(o=>o.OrderDate).Take(10).Select(o=>new{o.OrderId,o.OrderDate,status=o.Status.Name,o.TotalAmount,customer=o.User.Email}).ToListAsync(ct);var low=await db.ProductVariants.AsNoTracking().Where(v=>v.StockQty<=5).OrderBy(v=>v.StockQty).Take(100).Select(v=>new{v.VariantId,v.SKU,v.StockQty,product=v.Product.Name,size=v.Size.Label,color=v.Color.Name}).ToListAsync(ct);return Ok(new{ordersByStatus=byStatus,ordersToday=await db.Orders.CountAsync(o=>o.OrderDate>=today,ct),lowStockVariants=low,recentOrders=recent});}

    [HttpGet("~/api/admin/dashboard"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AdminDashboard(CancellationToken ct)
    {var start=DateTime.UtcNow.Date.AddDays(-29);var sales=await db.Orders.AsNoTracking().Where(o=>o.OrderDate>=start&&o.Status.Name!="Cancelled").GroupBy(o=>o.OrderDate.Date).Select(g=>new{date=g.Key,revenue=g.Sum(o=>o.TotalAmount)}).ToListAsync(ct);var revenue=await db.Orders.AsNoTracking().Where(o=>o.Status.Name!="Cancelled").GroupBy(_=>1).Select(g=>new{today=g.Where(o=>o.OrderDate>=DateTime.UtcNow.Date).Sum(o=>o.TotalAmount),last7=g.Where(o=>o.OrderDate>=DateTime.UtcNow.Date.AddDays(-6)).Sum(o=>o.TotalAmount),last30=g.Where(o=>o.OrderDate>=start).Sum(o=>o.TotalAmount)}).SingleOrDefaultAsync(ct);var top=await db.OrderItems.AsNoTracking().Where(i=>i.Order.Status.Name!="Cancelled").GroupBy(i=>new{i.Variant.ProductId,i.Variant.Product.Name}).Select(g=>new{productId=g.Key.ProductId,name=g.Key.Name,units=g.Sum(i=>i.Quantity)}).OrderByDescending(x=>x.units).Take(5).ToListAsync(ct);return Ok(new{baseDashboard=await Dashboard(ct),revenue=new{today=revenue?.today??0,last7=revenue?.last7??0,last30=revenue?.last30??0},dailyRevenue=Enumerable.Range(0,30).Select(i=>{var d=start.AddDays(i);return new{date=d,revenue=sales.Where(x=>x.date==d).Sum(x=>x.revenue)};}),topProducts=top});}

    [HttpGet("categories"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Categories(CancellationToken ct)=>Ok(await db.Categories.AsNoTracking().OrderBy(c=>c.Name).Select(c=>new{c.CategoryId,c.ParentCategoryId,c.Name}).ToListAsync(ct));
    [HttpPost("categories"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateCategory(CategoryRequest r,[FromServices]IValidator<CategoryRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;if(r.ParentCategoryId.HasValue&&!await db.Categories.AnyAsync(c=>c.CategoryId==r.ParentCategoryId,ct))return ValidationProblem(new Dictionary<string,string[]>{{"parentCategoryId",["Parent category does not exist."]}});var c=new Category{Name=r.Name.Trim(),ParentCategoryId=r.ParentCategoryId};db.Categories.Add(c);await db.SaveChangesAsync(ct);Log("category.create",c.CategoryId);return Created($"/api/manage/categories/{c.CategoryId}",new{c.CategoryId,c.ParentCategoryId,c.Name});}
    [HttpPut("categories/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateCategory(int id,CategoryRequest r,[FromServices]IValidator<CategoryRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var c=await db.Categories.FindAsync([id],ct);if(c==null)return NotFound();if(r.ParentCategoryId==id)return ConflictProblem("A category cannot be its own parent.");if(r.ParentCategoryId.HasValue){var parent=await db.Categories.FindAsync([r.ParentCategoryId.Value],ct);if(parent==null)return ValidationProblem(new Dictionary<string,string[]>{{"parentCategoryId",["Parent category does not exist."]}});while(parent!=null){if(parent.CategoryId==id)return ConflictProblem("Category hierarchy cannot contain a cycle.");parent=parent.ParentCategoryId.HasValue?await db.Categories.FindAsync([parent.ParentCategoryId.Value],ct):null;}}c.Name=r.Name.Trim();c.ParentCategoryId=r.ParentCategoryId;await db.SaveChangesAsync(ct);Log("category.update",id);return Ok(new{c.CategoryId,c.ParentCategoryId,c.Name});}
    [HttpDelete("categories/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> DeleteCategory(int id,CancellationToken ct){var c=await db.Categories.FindAsync([id],ct);if(c==null)return NotFound();if(await db.Categories.AnyAsync(x=>x.ParentCategoryId==id,ct)||await db.Products.AnyAsync(x=>x.CategoryId==id,ct))return ConflictProblem("Category has products or child categories and cannot be deleted.");db.Categories.Remove(c);await db.SaveChangesAsync(ct);Log("category.delete",id);return NoContent();}
    [HttpGet("sizes"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Sizes(CancellationToken ct)=>Ok(await db.Sizes.AsNoTracking().OrderBy(s=>s.Label).Select(s=>new{s.SizeId,s.Label}).ToListAsync(ct));
    [HttpPost("sizes"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateSize(NameRequest r,[FromServices]IValidator<NameRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var s=new Size{Label=r.Name.Trim()};db.Sizes.Add(s);await db.SaveChangesAsync(ct);Log("size.create",s.SizeId);return Created($"/api/manage/sizes/{s.SizeId}",new{s.SizeId,s.Label});}
    [HttpPut("sizes/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateSize(int id,NameRequest r,[FromServices]IValidator<NameRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var s=await db.Sizes.FindAsync([id],ct);if(s==null)return NotFound();s.Label=r.Name.Trim();await db.SaveChangesAsync(ct);Log("size.update",id);return Ok(new{s.SizeId,s.Label});}
    [HttpDelete("sizes/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> DeleteSize(int id,CancellationToken ct){var s=await db.Sizes.FindAsync([id],ct);if(s==null)return NotFound();if(await db.ProductVariants.AnyAsync(v=>v.SizeId==id,ct))return ConflictProblem("Size is used by product variants.");db.Sizes.Remove(s);await db.SaveChangesAsync(ct);Log("size.delete",id);return NoContent();}
    [HttpGet("colors"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Colors(CancellationToken ct)=>Ok(await db.Colors.AsNoTracking().OrderBy(c=>c.Name).Select(c=>new{c.ColorId,c.Name,c.HexCode}).ToListAsync(ct));
    [HttpPost("colors"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> CreateColor(ColorRequest r,[FromServices]IValidator<ColorRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var c=new Color{Name=r.Name.Trim(),HexCode=r.HexCode};db.Colors.Add(c);await db.SaveChangesAsync(ct);Log("color.create",c.ColorId);return Created($"/api/manage/colors/{c.ColorId}",new{c.ColorId,c.Name,c.HexCode});}
    [HttpPut("colors/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> UpdateColor(int id,ColorRequest r,[FromServices]IValidator<ColorRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var c=await db.Colors.FindAsync([id],ct);if(c==null)return NotFound();c.Name=r.Name.Trim();c.HexCode=r.HexCode;await db.SaveChangesAsync(ct);Log("color.update",id);return Ok(new{c.ColorId,c.Name,c.HexCode});}
    [HttpDelete("colors/{id:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> DeleteColor(int id,CancellationToken ct){var c=await db.Colors.FindAsync([id],ct);if(c==null)return NotFound();if(await db.ProductVariants.AnyAsync(v=>v.ColorId==id,ct))return ConflictProblem("Color is used by product variants.");db.Colors.Remove(c);await db.SaveChangesAsync(ct);Log("color.delete",id);return NoContent();}

    [HttpPost("products/{id:int}/images"), Authorize(Policy = "StaffOnly")]
    [RequestSizeLimit(31_500_000)]
    public async Task<IActionResult> UploadImages(int id,[FromForm]List<IFormFile> files,CancellationToken ct)
    {var p=await db.Products.FindAsync([id],ct);if(p==null)return NotFound();if(files.Count is <1 or >6)return ValidationProblem(new Dictionary<string,string[]>{{"files",["Upload between 1 and 6 images."]}});var saved=new List<string>();var rows=new List<ProductImage>();try{var dir=Path.Combine(env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot"),"images",SafePath(p.ProductCode));Directory.CreateDirectory(dir);foreach(var f in files){if(f.Length is <=0 or >5*1024*1024)throw new InvalidDataException("Each image must be between 1 byte and 5 MB.");var ext=ImageExtension(f);if(ext==null)throw new InvalidDataException("Only jpg, jpeg, png and webp images with matching content type and signature are allowed.");var name=Guid.NewGuid().ToString("N")+ext;var path=Path.Combine(dir,name);await using(var stream=System.IO.File.Create(path))await f.CopyToAsync(stream,ct);saved.Add(path);rows.Add(new ProductImage{ProductId=id,Url=$"/images/{SafePath(p.ProductCode)}/{name}",SortOrder=(await db.ProductImages.CountAsync(i=>i.ProductId==id,ct))+rows.Count});}if(!await db.ProductImages.AnyAsync(i=>i.ProductId==id,ct))rows[0].IsPrimary=true;db.ProductImages.AddRange(rows);await db.SaveChangesAsync(ct);}catch(InvalidDataException ex){foreach(var f in saved)System.IO.File.Delete(f);return ValidationProblem(new Dictionary<string,string[]>{{"files",[ex.Message]}});}catch{foreach(var f in saved)if(System.IO.File.Exists(f))System.IO.File.Delete(f);throw;}Log("product.images.upload",id);return Ok(rows.Select(i=>new{i.ImageId,i.Url,i.IsPrimary,i.SortOrder}));}

    [HttpPut("products/{id:int}/images/{imageId:int}/primary"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Primary(int id,int imageId,CancellationToken ct){await using var tx=await db.Database.BeginTransactionAsync(ct);var rows=await db.ProductImages.Where(i=>i.ProductId==id).ToListAsync(ct);var selected=rows.SingleOrDefault(i=>i.ImageId==imageId);if(selected==null)return NotFound();foreach(var i in rows.Where(i=>i.IsPrimary))i.IsPrimary=false;await db.SaveChangesAsync(ct);selected.IsPrimary=true;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);Log("product.image.primary",imageId);return NoContent();}
    [HttpPut("products/{id:int}/images/reorder"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Reorder(int id,ImageOrderRequest r,[FromServices]IValidator<ImageOrderRequest> validator,CancellationToken ct){var e=await Validate(validator,r,ct);if(e!=null)return e;var rows=await db.ProductImages.Where(i=>i.ProductId==id).ToListAsync(ct);if(rows.Count!=r.ImageIds.Distinct().Count()||rows.Any(i=>!r.ImageIds.Contains(i.ImageId)))return ValidationProblem(new Dictionary<string,string[]>{{"imageIds",["Provide every image id exactly once for this product."]}});for(var n=0;n<r.ImageIds.Count;n++)rows.Single(i=>i.ImageId==r.ImageIds[n]).SortOrder=n;await db.SaveChangesAsync(ct);Log("product.images.reorder",id);return NoContent();}
    [HttpDelete("products/{id:int}/images/{imageId:int}"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> DeleteImage(int id,int imageId,CancellationToken ct){var row=await db.ProductImages.SingleOrDefaultAsync(i=>i.ProductId==id&&i.ImageId==imageId,ct);if(row==null)return NotFound();var wasPrimary=row.IsPrimary;var next=wasPrimary?await db.ProductImages.Where(i=>i.ProductId==id&&i.ImageId!=imageId).OrderBy(i=>i.SortOrder).FirstOrDefaultAsync(ct):null;var path=PhysicalImagePath(row.Url);await using var tx=await db.Database.BeginTransactionAsync(ct);db.ProductImages.Remove(row);await db.SaveChangesAsync(ct);if(next!=null){next.IsPrimary=true;await db.SaveChangesAsync(ct);}await tx.CommitAsync(ct);if(path!=null&&System.IO.File.Exists(path))System.IO.File.Delete(path);Log("product.image.delete",imageId);return NoContent();}

    [HttpGet("products/{id:int}/images"), Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> Images(int id,CancellationToken ct){if(!await db.Products.AnyAsync(p=>p.ProductId==id,ct))return NotFound();return Ok(await db.ProductImages.AsNoTracking().Where(i=>i.ProductId==id).OrderBy(i=>i.SortOrder).Select(i=>new{i.ImageId,i.Url,i.IsPrimary,i.SortOrder}).ToListAsync(ct));}

    private string? PhysicalImagePath(string url){if(!url.StartsWith("/images/",StringComparison.Ordinal))return null;var root=Path.GetFullPath(env.WebRootPath??Path.Combine(env.ContentRootPath,"wwwroot"));var path=Path.GetFullPath(Path.Combine(root,url.TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));return path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?path:null;}
    private static string SafePath(string x)=>new string(x.Where(char.IsLetterOrDigit).ToArray());
    private static string? ImageExtension(IFormFile f){var b=new byte[12];using var s=f.OpenReadStream();var n=s.Read(b);var ct=f.ContentType.ToLowerInvariant();if(n>=3&&b[0]==0xff&&b[1]==0xd8&&b[2]==0xff&&ct=="image/jpeg")return ".jpg";if(n>=8&&b.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&ct=="image/png")return ".png";if(n>=12&&System.Text.Encoding.ASCII.GetString(b,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(b,8,4)=="WEBP"&&ct=="image/webp")return ".webp";return null;}
    private void Log(string action,int id)=>log.LogInformation("Management action {Action} by user {UserId} on {EntityId}",action,User.GetUserId(),id);
    private static object ProductDto(Product p)=>new{p.ProductId,p.ProductCode,p.Name,p.Description,p.CategoryId,p.BasePrice,p.IsActive};
    private static object VariantDto(ProductVariant v)=>new{v.VariantId,v.ProductId,v.SizeId,v.ColorId,v.SKU,v.PriceOverride,v.StockQty,v.RowVersion};
    private static string[] Allowed(string status)=>status switch{"Pending"=>["Paid","Cancelled"],"Paid"=>["Shipped","Cancelled"],"Shipped"=>["Delivered"],_=>[]};
    private bool TrySetVersion(ProductVariant variant,string token){try{var original=Convert.FromBase64String(token);if(original.Length!=8)return false;db.Entry(variant).Property(x=>x.RowVersion).OriginalValue=original;return true;}catch{return false;}}
    private static async Task<BadRequestObjectResult?> Validate<T>(IValidator<T> v,T request,CancellationToken ct){var result=await v.ValidateAsync(request,ct);return result.IsValid?null:new BadRequestObjectResult(new ValidationProblemDetails(result.Errors.GroupBy(e=>e.PropertyName).ToDictionary(g=>g.Key,g=>g.Select(e=>e.ErrorMessage).ToArray())));}
    private ConflictObjectResult ConflictProblem(string detail)=>Conflict(new ProblemDetails{Status=409,Title="Conflict",Detail=detail});
    private BadRequestObjectResult ValidationProblem(Dictionary<string,string[]> errors)=>new(new ValidationProblemDetails(errors){Status=400});
}

public sealed record ProductRequest(string ProductCode,string Name,string? Description,int CategoryId,decimal BasePrice);
public sealed record ActiveRequest(bool IsActive);
public sealed record VariantCreateRequest(int SizeId,int ColorId,string? Sku,decimal? PriceOverride,int StockQty);
public sealed record VariantUpdateRequest(decimal? PriceOverride,int StockQty,string RowVersion);
public sealed record StockRequest(long? Delta,int? Set,string RowVersion);
public sealed record StatusRequest(string Status);
public sealed record ImageOrderRequest(IReadOnlyList<int> ImageIds);
public sealed record CategoryRequest(string Name,int? ParentCategoryId);
public sealed record NameRequest(string Name);
public sealed record ColorRequest(string Name,string? HexCode);

public sealed class ProductRequestValidator:AbstractValidator<ProductRequest>{public ProductRequestValidator(){RuleFor(x=>x.ProductCode).NotEmpty().MaximumLength(50);RuleFor(x=>x.Name).NotEmpty().MaximumLength(200);RuleFor(x=>x.Description).MaximumLength(4000);RuleFor(x=>x.CategoryId).GreaterThan(0);RuleFor(x=>x.BasePrice).GreaterThan(0).LessThanOrEqualTo(10000000);}}
public sealed class ActiveRequestValidator:AbstractValidator<ActiveRequest>{public ActiveRequestValidator(){RuleFor(x=>x.IsActive).NotNull();}}
public sealed class VariantCreateRequestValidator:AbstractValidator<VariantCreateRequest>{public VariantCreateRequestValidator(){RuleFor(x=>x.SizeId).GreaterThan(0);RuleFor(x=>x.ColorId).GreaterThan(0);RuleFor(x=>x.Sku).MaximumLength(100);RuleFor(x=>x.PriceOverride).GreaterThan(0).When(x=>x.PriceOverride.HasValue);RuleFor(x=>x.StockQty).GreaterThanOrEqualTo(0);}}
public sealed class VariantUpdateRequestValidator:AbstractValidator<VariantUpdateRequest>{public VariantUpdateRequestValidator(){RuleFor(x=>x.PriceOverride).GreaterThan(0).When(x=>x.PriceOverride.HasValue);RuleFor(x=>x.StockQty).GreaterThanOrEqualTo(0);RuleFor(x=>x.RowVersion).NotEmpty();}}
public sealed class StockRequestValidator:AbstractValidator<StockRequest>{public StockRequestValidator(){RuleFor(x=>x).Must(x=>(x.Set.HasValue ^ x.Delta.HasValue)).WithMessage("Supply exactly one of delta or set.");RuleFor(x=>x.Set).GreaterThanOrEqualTo(0).When(x=>x.Set.HasValue);RuleFor(x=>x.RowVersion).NotEmpty();}}
public sealed class StatusRequestValidator:AbstractValidator<StatusRequest>{public StatusRequestValidator(){RuleFor(x=>x.Status).NotEmpty().MaximumLength(30);}}
public sealed class ImageOrderRequestValidator:AbstractValidator<ImageOrderRequest>{public ImageOrderRequestValidator(){RuleFor(x=>x.ImageIds).NotNull().Must(x=>x.Count<=100).WithMessage("Too many image ids.");}}
public sealed class CategoryRequestValidator:AbstractValidator<CategoryRequest>{public CategoryRequestValidator(){RuleFor(x=>x.Name).NotEmpty().MaximumLength(100);RuleFor(x=>x.ParentCategoryId).GreaterThan(0).When(x=>x.ParentCategoryId.HasValue);}}
public sealed class NameRequestValidator:AbstractValidator<NameRequest>{public NameRequestValidator(){RuleFor(x=>x.Name).NotEmpty().MaximumLength(30);}}
public sealed class ColorRequestValidator:AbstractValidator<ColorRequest>{public ColorRequestValidator(){RuleFor(x=>x.Name).NotEmpty().MaximumLength(100);RuleFor(x=>x.HexCode).Matches("^#[0-9a-fA-F]{6}$").When(x=>!string.IsNullOrWhiteSpace(x.HexCode));}}
