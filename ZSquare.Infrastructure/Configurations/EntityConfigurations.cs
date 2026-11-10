using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZSquare.Domain;

namespace ZSquare.Infrastructure.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.HasKey(x => x.RoleId);
        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.UserId);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.PasswordHash).IsRequired();
        b.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.HasKey(x => x.OrderStatusHistoryId);
        b.Property(x => x.FromStatus).HasMaxLength(30).IsRequired();
        b.Property(x => x.ToStatus).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.OrderId, x.ChangedAt });
        b.HasOne(x => x.Order).WithMany(x => x.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.TokenId);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.HasKey(x => x.AddressId);
        b.Property(x => x.Line1).HasMaxLength(200).IsRequired();
        b.Property(x => x.Line2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.Country).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.User).WithMany(x => x.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.CategoryId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.ParentCategory).WithMany(x => x.Children).HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.ProductId);
        b.HasIndex(x => x.ProductCode).IsUnique();
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.Name);
        b.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.BasePrice).HasPrecision(10, 2);
        b.ToTable(t => t.HasCheckConstraint("CK_Products_BasePrice", "[BasePrice] >= 0"));
        b.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.HasKey(x => x.ImageId);
        b.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        b.HasIndex(x => x.ProductId).IsUnique().HasFilter("[IsPrimary] = 1");
        b.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SizeConfiguration : IEntityTypeConfiguration<Size>
{
    public void Configure(EntityTypeBuilder<Size> b)
    {
        b.HasKey(x => x.SizeId);
        b.Property(x => x.Label).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Label).IsUnique();
    }
}

public sealed class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> b)
    {
        b.HasKey(x => x.ColorId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.HexCode).HasMaxLength(7);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.HasKey(x => x.VariantId);
        b.HasIndex(x => x.SKU).IsUnique();
        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => new { x.ProductId, x.SizeId, x.ColorId }).IsUnique();
        b.Property(x => x.SKU).HasMaxLength(100).IsRequired();
        b.Property(x => x.PriceOverride).HasPrecision(10, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_StockQty", "[StockQty] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_PriceOverride", "[PriceOverride] IS NULL OR [PriceOverride] >= 0");
        });
        b.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Size).WithMany(x => x.Variants).HasForeignKey(x => x.SizeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Color).WithMany(x => x.Variants).HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.HasKey(x => x.CartId);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithOne(x => x.Cart).HasForeignKey<Cart>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasKey(x => x.CartItemId);
        b.HasIndex(x => new { x.CartId, x.VariantId }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] > 0"));
        b.HasOne(x => x.Cart).WithMany(x => x.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Variant).WithMany(x => x.CartItems).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatus>
{
    public void Configure(EntityTypeBuilder<OrderStatus> b)
    {
        b.HasKey(x => x.StatusId);
        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(x => x.OrderId);
        b.HasIndex(x => new { x.UserId, x.OrderDate });
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        b.Property(x => x.ShipName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ShipLine1).HasMaxLength(200).IsRequired();
        b.Property(x => x.ShipLine2).HasMaxLength(200);
        b.Property(x => x.ShipCity).HasMaxLength(100).IsRequired();
        b.Property(x => x.ShipPostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.ShipCountry).HasMaxLength(100).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(10, 2);
        b.ToTable(t => t.HasCheckConstraint("CK_Orders_TotalAmount", "[TotalAmount] >= 0"));
        b.HasOne(x => x.User).WithMany(x => x.Orders).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Status).WithMany(x => x.Orders).HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.HasKey(x => x.OrderItemId);
        b.HasIndex(x => new { x.OrderId, x.VariantId }).IsUnique();
        b.Property(x => x.UnitPrice).HasPrecision(10, 2);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
        });
        b.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Variant).WithMany(x => x.OrderItems).HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(x => x.PaymentId);
        b.Property(x => x.Method).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(50).IsRequired();
        b.Property(x => x.TransactionRef).HasMaxLength(200);
        b.Property(x => x.Amount).HasPrecision(10, 2);
        b.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0"));
        b.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.HasKey(x => x.ReviewId);
        b.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.HasIndex(x => new { x.ProductId, x.IsHidden, x.CreatedAt });
        b.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
        b.HasOne(x => x.Product).WithMany(x => x.Reviews).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany(x => x.Reviews).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ModeratedBy).WithMany().HasForeignKey(x => x.ModeratedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
