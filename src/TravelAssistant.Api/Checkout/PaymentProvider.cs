namespace TravelAssistant.Api.Checkout;

// Stripe-style payment provider abstraction. The fake implementation lives in tests;
// production binds to a real provider via DI.
public interface IPaymentProvider
{
    Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default);
}

public sealed record ChargeRequest(string PaymentToken, long AmountCents, string Currency);

public abstract record PaymentResult
{
    public sealed record Approved(string ProviderChargeId) : PaymentResult;
    public sealed record DeclinedResult(string ProviderCode, string Message) : PaymentResult;

    public static PaymentResult Of(string chargeId) => new Approved(chargeId);
    public static PaymentResult Declined(string providerCode, string message)
        => new DeclinedResult(providerCode, message);
}

// Default in-memory provider used when no real provider is bound (dev only).
internal sealed class AlwaysApprovePaymentProvider : IPaymentProvider
{
    public Task<PaymentResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<PaymentResult>(new PaymentResult.Approved($"ch_dev_{Guid.NewGuid():N}"));
}
