namespace TravelAssistant.Api.Checkout;

// API DTOs ------------------------------------------------------------
public sealed record CartItemDto(string Sku, int Quantity, int UnitPriceCents);

public sealed record CheckoutRequest(
    string CustomerEmail,
    IReadOnlyList<CartItemDto> Items,
    string PaymentToken,
    string Currency);

public sealed record CheckoutResponse(Guid OrderId, string Status, long TotalCents);

public sealed record OrderItemDto(string Sku, int Quantity, int UnitPriceCents);

public sealed record OrderDto(
    Guid Id,
    string CustomerEmail,
    string Status,
    string Currency,
    long TotalCents,
    IReadOnlyList<OrderItemDto> Items);

public sealed record ProblemDto(string Code, string Message);

// Domain --------------------------------------------------------------
public enum OrderStatus { Cart, Details, Payment, Confirmed, Failed, Abandoned }

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string CustomerEmail { get; init; }
    public required string Currency { get; init; }
    public required long TotalCents { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Cart;
    public string? PaymentChargeId { get; set; }
    public List<OrderItem> Items { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class OrderItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required int UnitPriceCents { get; init; }
}
