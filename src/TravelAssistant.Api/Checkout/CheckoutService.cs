namespace TravelAssistant.Api.Checkout;

// Drives the checkout state machine: Cart -> Details -> Payment -> Confirmed
// (terminals: Failed, Abandoned). Idempotency-Key replay is handled here so
// the endpoint stays a thin shell.
public sealed class CheckoutService
{
    private readonly IPaymentProvider _payments;
    private readonly IIdempotencyStore _idempotency;
    private readonly IOrderStore _orders;

    public CheckoutService(IPaymentProvider payments, IIdempotencyStore idempotency, IOrderStore orders)
    {
        _payments = payments;
        _idempotency = idempotency;
        _orders = orders;
    }

    public async Task<CheckoutOutcome> CheckoutAsync(
        CheckoutRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_idempotency.TryGet(idempotencyKey, out var cached))
        {
            return CheckoutOutcome.Replayed(cached);
        }

        var validation = Validate(request);
        if (validation is { } problem)
        {
            return CheckoutOutcome.Invalid(problem);
        }

        var total = request.Items.Sum(i => (long)i.Quantity * i.UnitPriceCents);

        var chargeResult = await _payments.ChargeAsync(
            new ChargeRequest(request.PaymentToken, total, request.Currency), ct);

        if (chargeResult is PaymentResult.DeclinedResult declined)
        {
            // No order persisted on decline — terminal Failed state is observed by clients
            // via the 402 response, not a stored Order row.
            return CheckoutOutcome.Declined(new ProblemDto(declined.ProviderCode, declined.Message));
        }

        var charge = (PaymentResult.Approved)chargeResult;

        var order = new Order
        {
            CustomerEmail = request.CustomerEmail,
            Currency = request.Currency,
            TotalCents = total,
            Status = OrderStatus.Confirmed,
            PaymentChargeId = charge.ProviderChargeId,
            Items = request.Items.Select(i => new OrderItem
            {
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPriceCents = i.UnitPriceCents,
            }).ToList(),
        };

        _orders.Save(order);

        var response = new CheckoutResponse(order.Id, order.Status.ToString(), order.TotalCents);
        _idempotency.Save(idempotencyKey, response);
        return CheckoutOutcome.Confirmed(response);
    }

    private static ProblemDto? Validate(CheckoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            return new ProblemDto("invalid_request", "CustomerEmail is required.");
        if (string.IsNullOrWhiteSpace(request.Currency))
            return new ProblemDto("invalid_request", "Currency is required.");
        if (string.IsNullOrWhiteSpace(request.PaymentToken))
            return new ProblemDto("invalid_request", "PaymentToken is required.");
        if (request.Items is null || request.Items.Count == 0)
            return new ProblemDto("empty_cart", "Cart must contain at least one item.");
        if (request.Items.Any(i => i.Quantity <= 0))
            return new ProblemDto("invalid_quantity", "Item quantity must be positive.");
        if (request.Items.Any(i => i.UnitPriceCents < 0))
            return new ProblemDto("invalid_price", "Item unit price cannot be negative.");
        return null;
    }
}

public abstract record CheckoutOutcome
{
    public sealed record ConfirmedResult(CheckoutResponse Response) : CheckoutOutcome;
    public sealed record ReplayedResult(CheckoutResponse Response) : CheckoutOutcome;
    public sealed record DeclinedOutcome(ProblemDto Problem) : CheckoutOutcome;
    public sealed record InvalidResult(ProblemDto Problem) : CheckoutOutcome;

    public static CheckoutOutcome Confirmed(CheckoutResponse r) => new ConfirmedResult(r);
    public static CheckoutOutcome Replayed(CheckoutResponse r) => new ReplayedResult(r);
    public static CheckoutOutcome Declined(ProblemDto p) => new DeclinedOutcome(p);
    public static CheckoutOutcome Invalid(ProblemDto p) => new InvalidResult(p);
}
