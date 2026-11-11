using FluentValidation;
namespace ZSquare.Application.Commerce;

public sealed record CartDto(IReadOnlyList<CartLineDto> Items, decimal Subtotal);

public sealed record CartLineDto(
    int CartItemId,
    int VariantId,
    int ProductId,
    string ProductName,
    string Size,
    string Color,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool InStock,
    bool VariantActive,
    bool StockSufficient);

public sealed class AddCartItemRequest
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public sealed class SetCartItemQuantityRequest
{
    public int Quantity { get; set; }
}

public sealed record AddressDto(
    int AddressId,
    string Line1,
    string? Line2,
    string City,
    string PostalCode,
    string Country,
    bool IsDefault);

public sealed class AddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(request => request.Line1).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Line2).MaximumLength(200);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(request => request.Country).NotEmpty().MaximumLength(100);
    }
}

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.VariantId).GreaterThan(0);
        RuleFor(request => request.Quantity).InclusiveBetween(1, 10);
    }
}

public sealed class SetCartItemQuantityRequestValidator : AbstractValidator<SetCartItemQuantityRequest>
{
    public SetCartItemQuantityRequestValidator() => RuleFor(request => request.Quantity).InclusiveBetween(1, 10);
}

public sealed class CheckoutRequest
{
    public int AddressId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.AddressId).GreaterThan(0);
        RuleFor(request => request.PaymentMethod)
            .Must(method => method is "CashOnDelivery" or "MockCard")
            .WithMessage("Payment method must be CashOnDelivery or MockCard.");
    }
}

public sealed record CheckoutResult(OrderSummaryDto Order, bool Replayed);

public sealed record OrderSummaryDto(
    int OrderId,
    string Status,
    DateTime OrderDate,
    decimal TotalAmount,
    int ItemCount,
    string? FirstItemImageUrl);

public sealed record OrderPageDto(
    IReadOnlyList<OrderSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record OrderItemDto(
    int VariantId,
    int ProductId,
    string ProductName,
    string Size,
    string Color,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDetailsDto(
    int OrderId,
    string Status,
    DateTime OrderDate,
    DateTime? DeliveredAt,
    decimal TotalAmount,
    string ShipName,
    string ShipLine1,
    string? ShipLine2,
    string ShipCity,
    string ShipPostalCode,
    string ShipCountry,
    IReadOnlyList<OrderItemDto> Items);

public sealed record CommerceError(int StatusCode, string Title, IReadOnlyDictionary<string, string[]> Errors);

public sealed record CommerceResult<T>(T? Value, CommerceError? Error)
{
    public bool Succeeded => Error is null;
    public static CommerceResult<T> Success(T value) => new(value, null);
    public static CommerceResult<T> Failure(int statusCode, string title, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(default, new CommerceError(statusCode, title, errors ?? new Dictionary<string, string[]>()));
}

public interface ICustomerCommerceService
{
    Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default);
    Task<CommerceResult<CartDto>> AddCartItemAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CommerceResult<CartDto>> SetCartItemQuantityAsync(int userId, int cartItemId, int quantity, CancellationToken cancellationToken = default);
    Task<CommerceResult<bool>> RemoveCartItemAsync(int userId, int cartItemId, CancellationToken cancellationToken = default);
    Task ClearCartAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int userId, CancellationToken cancellationToken = default);
    Task<AddressDto> AddAddressAsync(int userId, AddressRequest request, CancellationToken cancellationToken = default);
    Task<CommerceResult<AddressDto>> UpdateAddressAsync(int userId, int addressId, AddressRequest request, CancellationToken cancellationToken = default);
    Task<CommerceResult<bool>> DeleteAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default);

    Task<CommerceResult<CheckoutResult>> CheckoutAsync(int userId, CheckoutRequest request, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<OrderPageDto> GetOrdersAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto?> GetOrderAsync(int userId, int orderId, CancellationToken cancellationToken = default);
    Task<CommerceResult<OrderSummaryDto>> CancelOrderAsync(int userId, int orderId, CancellationToken cancellationToken = default);
}
