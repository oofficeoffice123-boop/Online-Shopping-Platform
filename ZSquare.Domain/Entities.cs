namespace ZSquare.Domain;

public static class PakistanClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
            return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);
        }
    }

    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi"); }
    }
}

public sealed class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public ICollection<User> Users { get; set; } = [];
}

public sealed class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Address> Addresses { get; set; } = [];
    public Cart? Cart { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}

public sealed class RefreshToken
{
    public int TokenId { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
}

public sealed class Address
{
    public int AddressId { get; set; }
    public int UserId { get; set; }
    public string Line1 { get; set; } = null!;
    public string? Line2 { get; set; }
    public string City { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string Country { get; set; } = null!;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
}

public sealed class Category
{
    public int CategoryId { get; set; }
    public int? ParentCategoryId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public int CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}

public sealed class ProductImage
{
    public int ImageId { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Product Product { get; set; } = null!;
}

public sealed class Size
{
    public int SizeId { get; set; }
    public string Label { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];
}

public sealed class Color
{
    public int ColorId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];
}

public sealed class ProductVariant
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public int SizeId { get; set; }
    public int ColorId { get; set; }
    public string SKU { get; set; } = null!;
    public decimal? PriceOverride { get; set; }
    public int StockQty { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public Product Product { get; set; } = null!;
    public Size Size { get; set; } = null!;
    public Color Color { get; set; } = null!;
    public ICollection<CartItem> CartItems { get; set; } = [];
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}

public sealed class Cart
{
    public int CartId { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
    public ICollection<CartItem> Items { get; set; } = [];
}

public sealed class CartItem
{
    public int CartItemId { get; set; }
    public int CartId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Cart Cart { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

public sealed class OrderStatus
{
    public int StatusId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
}

public sealed class Order
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public int StatusId { get; set; }
    public DateTime OrderDate { get; set; }
    public string ShipName { get; set; } = null!;
    public string ShipLine1 { get; set; } = null!;
    public string? ShipLine2 { get; set; }
    public string ShipCity { get; set; } = null!;
    public string ShipPostalCode { get; set; } = null!;
    public string ShipCountry { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
    public OrderStatus Status { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
}

public sealed class OrderStatusHistory
{
    public int OrderStatusHistoryId { get; set; }
    public int OrderId { get; set; }
    public string FromStatus { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public Order Order { get; set; } = null!;
    public User ChangedBy { get; set; } = null!;
}

public sealed class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Order Order { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

public sealed class Payment
{
    public int PaymentId { get; set; }
    public int UserId { get; set; }
    public int OrderId { get; set; }
    public string Method { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? TransactionRef { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public Order Order { get; set; } = null!;
}

public sealed class Review
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsHidden { get; set; }
    public int? ModeratedByUserId { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? ModeratedBy { get; set; }
}
