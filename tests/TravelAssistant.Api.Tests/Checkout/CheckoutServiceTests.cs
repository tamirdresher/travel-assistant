using System.Threading.Tasks;
using TravelAssistant.Api.Checkout;
using Xunit;

namespace TravelAssistant.Api.Tests.Checkout;

[Trait("Category", "Unit")]
public class CheckoutServiceTests
{
    private static CheckoutService NewService(IPaymentProvider? provider = null, IOrderStore? orders = null)
        => new(
            provider ?? new FakePaymentProvider(),
            new InMemoryIdempotencyStoreTestAccess(),
            orders ?? new InMemoryOrderStoreTestAccess());

    [Fact]
    public async Task Checkout_with_valid_cart_confirms_order_and_sums_total()
    {
        var svc = NewService();
        var req = new CheckoutRequest(
            "ada@example.com",
            new[] { new CartItemDto("SKU-1", 2, 1_999), new CartItemDto("SKU-2", 1, 500) },
            "tok_ok",
            "USD");

        var outcome = await svc.CheckoutAsync(req, "key-1");

        var confirmed = Assert.IsType<CheckoutOutcome.ConfirmedResult>(outcome);
        Assert.Equal("Confirmed", confirmed.Response.Status);
        Assert.Equal(2 * 1_999 + 500, confirmed.Response.TotalCents);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_cached_response_and_does_not_recharge()
    {
        var provider = new FakePaymentProvider();
        var svc = NewService(provider);
        var req = new CheckoutRequest(
            "ada@example.com",
            new[] { new CartItemDto("SKU-1", 1, 1000) },
            "tok_ok",
            "USD");

        var first = await svc.CheckoutAsync(req, "same-key");
        var second = await svc.CheckoutAsync(req, "same-key");

        var firstResp = ((CheckoutOutcome.ConfirmedResult)first).Response;
        var replay = Assert.IsType<CheckoutOutcome.ReplayedResult>(second);
        Assert.Equal(firstResp.OrderId, replay.Response.OrderId);
        Assert.Equal(1, provider.ChargeAttempts);
    }

    [Fact]
    public async Task Declined_card_yields_declined_outcome_and_no_persisted_order()
    {
        var provider = new FakePaymentProvider { NextResult = PaymentResult.Declined("card_declined", "Your card was declined.") };
        var orders = new InMemoryOrderStoreTestAccess();
        var svc = NewService(provider, orders);

        var outcome = await svc.CheckoutAsync(
            new CheckoutRequest("alan@example.com",
                new[] { new CartItemDto("SKU-X", 1, 100) }, "tok_bad", "USD"),
            "key-decline");

        var declined = Assert.IsType<CheckoutOutcome.DeclinedOutcome>(outcome);
        Assert.Equal("card_declined", declined.Problem.Code);
        Assert.Empty(orders.ByEmail("alan@example.com"));
    }

    [Theory]
    [InlineData("", "tok", "USD", "invalid_request")]
    [InlineData("a@b.c", "", "USD", "invalid_request")]
    [InlineData("a@b.c", "tok", "", "invalid_request")]
    public async Task Missing_required_fields_returns_invalid(string email, string token, string currency, string expectedCode)
    {
        var svc = NewService();
        var outcome = await svc.CheckoutAsync(
            new CheckoutRequest(email, new[] { new CartItemDto("SKU", 1, 100) }, token, currency),
            "k");
        var invalid = Assert.IsType<CheckoutOutcome.InvalidResult>(outcome);
        Assert.Equal(expectedCode, invalid.Problem.Code);
    }

    [Fact]
    public async Task Empty_cart_is_rejected()
    {
        var svc = NewService();
        var outcome = await svc.CheckoutAsync(
            new CheckoutRequest("a@b.c", Array.Empty<CartItemDto>(), "tok", "USD"), "k");
        Assert.Equal("empty_cart", ((CheckoutOutcome.InvalidResult)outcome).Problem.Code);
    }

    [Fact]
    public async Task Zero_quantity_is_rejected()
    {
        var svc = NewService();
        var outcome = await svc.CheckoutAsync(
            new CheckoutRequest("a@b.c", new[] { new CartItemDto("SKU", 0, 100) }, "tok", "USD"), "k");
        Assert.Equal("invalid_quantity", ((CheckoutOutcome.InvalidResult)outcome).Problem.Code);
    }

    [Fact]
    public async Task Negative_price_is_rejected()
    {
        var svc = NewService();
        var outcome = await svc.CheckoutAsync(
            new CheckoutRequest("a@b.c", new[] { new CartItemDto("SKU", 1, -1) }, "tok", "USD"), "k");
        Assert.Equal("invalid_price", ((CheckoutOutcome.InvalidResult)outcome).Problem.Code);
    }
}

// Test doubles ----------------------------------------------------------
internal sealed class FakePaymentProvider : IPaymentProvider
{
    public PaymentResult NextResult { get; set; } = PaymentResult.Of("ch_test");
    public int ChargeAttempts { get; private set; }

    public Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default)
    {
        ChargeAttempts++;
        return Task.FromResult(NextResult);
    }
}

internal sealed class InMemoryIdempotencyStoreTestAccess : IIdempotencyStore
{
    private readonly Dictionary<string, CheckoutResponse> _store = new();
    public bool TryGet(string key, out CheckoutResponse response)
        => _store.TryGetValue(key, out response!);
    public void Save(string key, CheckoutResponse response) => _store[key] = response;
}

internal sealed class InMemoryOrderStoreTestAccess : IOrderStore
{
    private readonly Dictionary<Guid, Order> _orders = new();
    public void Save(Order order) => _orders[order.Id] = order;
    public Order? Get(Guid id) => _orders.TryGetValue(id, out var o) ? o : null;
    public IReadOnlyList<Order> ByEmail(string email)
        => _orders.Values.Where(o => string.Equals(o.CustomerEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
