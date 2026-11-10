using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShopEase.Application.Commerce;
using ShopEase.Domain;

namespace ShopEase.Infrastructure.Commerce;

public sealed class CustomerCommerceService(AppDbContext db) : ICustomerCommerceService
{
    public async Task<CartDto> GetCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.CartItems.AsNoTracking()
            .Where(item => item.Cart.UserId == userId)
            .OrderBy(item => item.CartItemId)
            .Select(item => new
            {
                item.CartItemId,
                item.VariantId,
                ProductId = item.Variant.ProductId,
                ProductName = item.Variant.Product.Name,
                Size = item.Variant.Size.Label,
                Color = item.Variant.Color.Name,
                ImageUrl = item.Variant.Product.Images.OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault(),
                UnitPrice = item.Variant.PriceOverride ?? item.Variant.Product.BasePrice,
                item.Quantity,
                item.Variant.Product.IsActive,
                item.Variant.StockQty
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row =>
        {
            var unitPrice = Money(row.UnitPrice);
            var active = row.IsActive;
            var enoughStock = row.StockQty >= row.Quantity;
            return new CartLineDto(
                row.CartItemId, row.VariantId, row.ProductId, row.ProductName, row.Size, row.Color, row.ImageUrl,
                unitPrice, row.Quantity, Money(unitPrice * row.Quantity), active && enoughStock,
                active, enoughStock);
        }).ToArray();
        return new CartDto(items, Money(items.Sum(item => item.LineTotal)));
    }

    public async Task<CommerceResult<CartDto>> AddCartItemAsync(
        int userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await db.ProductVariants.Include(item => item.Product)
            .SingleOrDefaultAsync(item => item.VariantId == request.VariantId, cancellationToken);
        if (variant is null)
            return CommerceResult<CartDto>.Failure(404, "Product variant not found.");
        if (!variant.Product.IsActive)
            return ItemFailure<CartDto>(409, "This product is no longer available.");

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        var cartItem = await db.CartItems.SingleOrDefaultAsync(
            item => item.CartId == cart.CartId && item.VariantId == request.VariantId, cancellationToken);
        var newQuantity = request.Quantity + (cartItem?.Quantity ?? 0);
        if (newQuantity > 10)
            return ItemFailure<CartDto>(400, "A cart line cannot contain more than 10 items.");
        if (variant.StockQty < newQuantity)
            return ItemFailure<CartDto>(409, "There is not enough stock for the requested quantity.");

        if (cartItem is null)
            db.CartItems.Add(new CartItem { CartId = cart.CartId, VariantId = variant.VariantId, Quantity = newQuantity });
        else
            cartItem.Quantity = newQuantity;
        await db.SaveChangesAsync(cancellationToken);
        return CommerceResult<CartDto>.Success(await GetCartAsync(userId, cancellationToken));
    }

    public async Task<CommerceResult<CartDto>> SetCartItemQuantityAsync(
        int userId, int cartItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await db.CartItems.Include(cartItem => cartItem.Cart)
            .Include(cartItem => cartItem.Variant).ThenInclude(variant => variant.Product)
            .SingleOrDefaultAsync(cartItem => cartItem.CartItemId == cartItemId && cartItem.Cart.UserId == userId, cancellationToken);
        if (item is null)
            return CommerceResult<CartDto>.Failure(404, "Cart item not found.");
        if (!item.Variant.Product.IsActive)
            return ItemFailure<CartDto>(409, "This product is no longer available.");
        if (item.Variant.StockQty < quantity)
            return ItemFailure<CartDto>(409, "There is not enough stock for the requested quantity.");

        item.Quantity = quantity;
        await db.SaveChangesAsync(cancellationToken);
        return CommerceResult<CartDto>.Success(await GetCartAsync(userId, cancellationToken));
    }

    public async Task<CommerceResult<bool>> RemoveCartItemAsync(int userId, int cartItemId, CancellationToken cancellationToken = default)
    {
        var item = await db.CartItems.Include(cartItem => cartItem.Cart)
            .SingleOrDefaultAsync(cartItem => cartItem.CartItemId == cartItemId && cartItem.Cart.UserId == userId, cancellationToken);
        if (item is null)
            return CommerceResult<bool>.Failure(404, "Cart item not found.");
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return CommerceResult<bool>.Success(true);
    }

    public async Task ClearCartAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (cart is null)
            return;
        var items = await db.CartItems.Where(item => item.CartId == cart.CartId).ToListAsync(cancellationToken);
        db.CartItems.RemoveRange(items);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.Addresses.AsNoTracking().Where(address => address.UserId == userId)
            .OrderByDescending(address => address.IsDefault).ThenBy(address => address.AddressId)
            .Select(address => ToAddressDto(address)).ToListAsync(cancellationToken);

    public async Task<AddressDto> AddAddressAsync(int userId, AddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = new Address { UserId = userId };
        ApplyAddress(address, request);
        var isFirst = !await db.Addresses.AnyAsync(item => item.UserId == userId, cancellationToken);
        if (request.IsDefault || isFirst)
            await SetDefaultAddressAsync(userId, address, cancellationToken);
        db.Addresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return ToAddressDto(address);
    }

    public async Task<CommerceResult<AddressDto>> UpdateAddressAsync(
        int userId, int addressId, AddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await db.Addresses.SingleOrDefaultAsync(
            item => item.AddressId == addressId && item.UserId == userId, cancellationToken);
        if (address is null)
            return CommerceResult<AddressDto>.Failure(404, "Address not found.");
        ApplyAddress(address, request);
        if (request.IsDefault)
            await SetDefaultAddressAsync(userId, address, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return CommerceResult<AddressDto>.Success(ToAddressDto(address));
    }

    public async Task<CommerceResult<bool>> DeleteAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default)
    {
        var address = await db.Addresses.SingleOrDefaultAsync(
            item => item.AddressId == addressId && item.UserId == userId, cancellationToken);
        if (address is null)
            return CommerceResult<bool>.Failure(404, "Address not found.");
        var wasDefault = address.IsDefault;
        db.Addresses.Remove(address);
        if (wasDefault)
        {
            var nextDefault = await db.Addresses.Where(item => item.UserId == userId && item.AddressId != addressId)
                .OrderBy(item => item.AddressId).FirstOrDefaultAsync(cancellationToken);
            if (nextDefault is not null)
                nextDefault.IsDefault = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return CommerceResult<bool>.Success(true);
    }

    public async Task<CommerceResult<CheckoutResult>> CheckoutAsync(
        int userId,
        CheckoutRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey?.Length > 100)
            return CommerceResult<CheckoutResult>.Failure(400, "Idempotency key must be at most 100 characters.",
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["The header must be at most 100 characters."] });

        if (idempotencyKey is not null)
        {
            var existing = await db.Orders.AsNoTracking().Where(order => order.UserId == userId && order.IdempotencyKey == idempotencyKey)
                .Select(order => order.OrderId).SingleOrDefaultAsync(cancellationToken);
            if (existing != 0)
                return CommerceResult<CheckoutResult>.Success(new CheckoutResult(
                    await GetOrderSummaryAsync(userId, existing, cancellationToken), true));
        }

        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        try
        {
            var cart = await db.Carts.Include(item => item.Items).ThenInclude(item => item.Variant)
                .ThenInclude(variant => variant.Product).ThenInclude(product => product.Images)
                .Include(item => item.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Size)
                .Include(item => item.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Color)
                .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (cart is null || cart.Items.Count == 0)
                return await FailAndRollbackAsync<CheckoutResult>(transaction, 400, "The cart is empty.", cancellationToken);

            var address = await db.Addresses.AsNoTracking().SingleOrDefaultAsync(
                item => item.AddressId == request.AddressId && item.UserId == userId, cancellationToken);
            if (address is null)
                return await FailAndRollbackAsync<CheckoutResult>(transaction, 400, "Choose one of your saved addresses.", cancellationToken,
                    new Dictionary<string, string[]> { ["addressId"] = ["The selected address does not belong to this account or no longer exists."] });

            var failures = new Dictionary<string, string[]>();
            foreach (var item in cart.Items)
            {
                if (!item.Variant.Product.IsActive)
                    failures[$"cartItem:{item.CartItemId}"] = [$"{item.Variant.Product.Name}: the product is no longer active."];
                else if (item.Variant.StockQty < item.Quantity)
                    failures[$"cartItem:{item.CartItemId}"] = [$"{item.Variant.Product.Name}: only {item.Variant.StockQty} remain, but the cart requests {item.Quantity}."];
            }
            if (failures.Count > 0)
                return await FailAndRollbackAsync<CheckoutResult>(transaction, 409,
                    "Some cart items changed and could not be checked out.", cancellationToken, failures);

            var now = DateTime.UtcNow;
            var pending = await db.OrderStatuses.SingleOrDefaultAsync(status => status.Name == "Pending", cancellationToken)
                ?? throw new InvalidOperationException("Pending order status is missing. Run reference-data seeding first.");
            var paid = request.PaymentMethod == "MockCard"
                ? await db.OrderStatuses.SingleOrDefaultAsync(status => status.Name == "Paid", cancellationToken)
                    ?? throw new InvalidOperationException("Paid order status is missing. Run reference-data seeding first.")
                : pending;
            var orderItems = cart.Items.Select(item =>
            {
                var unitPrice = Money(item.Variant.PriceOverride ?? item.Variant.Product.BasePrice);
                item.Variant.StockQty -= item.Quantity;
                return new OrderItem
                {
                    VariantId = item.VariantId,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice
                };
            }).ToArray();
            var total = Money(orderItems.Sum(item => item.UnitPrice * item.Quantity));
            var order = new Order
            {
                UserId = userId,
                StatusId = paid.StatusId,
                Status = paid,
                OrderDate = now,
                ShipName = (await db.Users.AsNoTracking().Where(user => user.UserId == userId)
                    .Select(user => user.FullName).SingleAsync(cancellationToken)).Trim(),
                ShipLine1 = address.Line1,
                ShipLine2 = address.Line2,
                ShipCity = address.City,
                ShipPostalCode = address.PostalCode,
                ShipCountry = address.Country,
                TotalAmount = total,
                IdempotencyKey = idempotencyKey,
                Items = orderItems,
                Payments =
                [
                    new Payment
                    {
                        Method = request.PaymentMethod,
                        Status = request.PaymentMethod == "MockCard" ? "Succeeded" : "Pending",
                        Amount = total,
                        TransactionRef = request.PaymentMethod == "MockCard" ? $"MOCK-{Guid.NewGuid():N}" : null,
                        PaidAt = request.PaymentMethod == "MockCard" ? now : null
                    }
                ]
            };
            db.Orders.Add(order);
            db.CartItems.RemoveRange(cart.Items);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return CommerceResult<CheckoutResult>.Success(new CheckoutResult(
                await GetOrderSummaryAsync(userId, order.OrderId, cancellationToken), false));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            var stockErrors = new Dictionary<string, string[]>
            {
                ["stock"] = ["Inventory changed while checkout was in progress. Refresh the cart and try again."]
            };
            return CommerceResult<CheckoutResult>.Failure(409, "Stock changed during checkout.", stockErrors);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            var existingId = await db.Orders.AsNoTracking()
                .Where(order => order.UserId == userId && order.IdempotencyKey == idempotencyKey)
                .Select(order => order.OrderId).SingleOrDefaultAsync(cancellationToken);
            if (existingId != 0)
                return CommerceResult<CheckoutResult>.Success(new CheckoutResult(
                    await GetOrderSummaryAsync(userId, existingId, cancellationToken), true));
            throw;
        }
    }

    public async Task<OrderPageDto> GetOrdersAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 48);
        var query = db.Orders.AsNoTracking().Where(order => order.UserId == userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(order => new OrderSummaryDto(
                order.OrderId, order.Status.Name, order.OrderDate, order.TotalAmount, order.Items.Count,
                order.Items.OrderBy(item => item.OrderItemId)
                    .Select(item => item.Variant.Product.Images.OrderByDescending(image => image.IsPrimary)
                        .ThenBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault())
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return new OrderPageDto(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<OrderDetailsDto?> GetOrderAsync(int userId, int orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking().Where(item => item.OrderId == orderId && item.UserId == userId)
            .Include(item => item.Status)
            .Include(item => item.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Product).ThenInclude(product => product.Images)
            .Include(item => item.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Size)
            .Include(item => item.Items).ThenInclude(item => item.Variant).ThenInclude(variant => variant.Color)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
            return null;

        var items = order.Items.OrderBy(item => item.OrderItemId).Select(item => new OrderItemDto(
            item.VariantId,
            item.Variant.Product.Name,
            item.Variant.Size.Label,
            item.Variant.Color.Name,
            item.Variant.Product.Images.OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault(),
            item.Quantity,
            item.UnitPrice,
            Money(item.UnitPrice * item.Quantity))).ToArray();
        var payments = order.Payments.OrderBy(item => item.PaymentId)
            .Select(item => new OrderPaymentDto(item.Method, item.Status, item.Amount, item.TransactionRef, item.PaidAt)).ToArray();
        return new OrderDetailsDto(order.OrderId, order.Status.Name, order.OrderDate, order.TotalAmount,
            order.ShipName, order.ShipLine1, order.ShipLine2, order.ShipCity, order.ShipPostalCode, order.ShipCountry, items, payments);
    }

    public async Task<CommerceResult<OrderSummaryDto>> CancelOrderAsync(int userId, int orderId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        try
        {
            var order = await db.Orders.Where(item => item.OrderId == orderId && item.UserId == userId)
                .Include(item => item.Status)
                .Include(item => item.Items).ThenInclude(item => item.Variant)
                .Include(item => item.Payments)
                .SingleOrDefaultAsync(cancellationToken);
            if (order is null)
                return await FailAndRollbackAsync<OrderSummaryDto>(transaction, 404, "Order not found.", cancellationToken);
            if (order.Status.Name != "Pending")
                return await FailAndRollbackAsync<OrderSummaryDto>(transaction, 409,
                    "Only pending orders can be cancelled.", cancellationToken,
                    new Dictionary<string, string[]> { ["status"] = [$"This order is {order.Status.Name}."] });

            var cancelled = await db.OrderStatuses.SingleAsync(status => status.Name == "Cancelled", cancellationToken);
            ApplyCancellation(order, cancelled);

            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return CommerceResult<OrderSummaryDto>.Success(await GetOrderSummaryAsync(userId, orderId, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            return CommerceResult<OrderSummaryDto>.Failure(409, "Stock changed while cancelling the order.",
                new Dictionary<string, string[]> { ["stock"] = ["Refresh the order and inventory before retrying."] });
        }
    }

    public static void ApplyCancellation(Order order, OrderStatus cancelled)
    {
        order.Status = cancelled;
        order.StatusId = cancelled.StatusId;
        foreach (var item in order.Items) item.Variant.StockQty += item.Quantity;
        foreach (var payment in order.Payments) payment.Status = payment.Method == "MockCard" ? "Refunded" : "Cancelled";
    }

    private async Task<Cart> GetOrCreateCartAsync(int userId, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (cart is not null)
            return cart;
        cart = new Cart { UserId = userId };
        db.Carts.Add(cart);
        await db.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task SetDefaultAddressAsync(int userId, Address selected, CancellationToken cancellationToken)
    {
        var addresses = await db.Addresses.Where(item => item.UserId == userId && item.AddressId != selected.AddressId)
            .ToListAsync(cancellationToken);
        foreach (var address in addresses)
            address.IsDefault = false;
        selected.IsDefault = true;
    }

    private static void ApplyAddress(Address address, AddressRequest request)
    {
        address.Line1 = request.Line1.Trim();
        address.Line2 = string.IsNullOrWhiteSpace(request.Line2) ? null : request.Line2.Trim();
        address.City = request.City.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
        address.IsDefault = request.IsDefault;
    }

    private static AddressDto ToAddressDto(Address address) =>
        new(address.AddressId, address.Line1, address.Line2, address.City, address.PostalCode, address.Country, address.IsDefault);

    private async Task<OrderSummaryDto> GetOrderSummaryAsync(int userId, int orderId, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking().Where(order => order.UserId == userId && order.OrderId == orderId)
            .Select(order => new OrderSummaryDto(
                order.OrderId, order.Status.Name, order.OrderDate, order.TotalAmount, order.Items.Count,
                order.Items.OrderBy(item => item.OrderItemId)
                    .Select(item => item.Variant.Product.Images.OrderByDescending(image => image.IsPrimary)
                        .ThenBy(image => image.SortOrder).Select(image => image.Url).FirstOrDefault())
                    .FirstOrDefault()))
            .SingleAsync(cancellationToken);

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

    private static async Task<CommerceResult<T>> FailAndRollbackAsync<T>(
        IDbContextTransaction? transaction,
        int status,
        string title,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        if (transaction is not null)
            await transaction.RollbackAsync(cancellationToken);
        return CommerceResult<T>.Failure(status, title, errors);
    }

    private static CommerceResult<T> ItemFailure<T>(int status, string message) =>
        CommerceResult<T>.Failure(status, message, new Dictionary<string, string[]> { ["item"] = [message] });

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
