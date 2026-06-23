using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TravelAssistant.Api.Checkout;

public static class CheckoutEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IServiceCollection AddCheckout(this IServiceCollection services)
    {
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
        // Only register the fake if no real provider has been bound by the host.
        services.TryAddSingleton<IPaymentProvider, AlwaysApprovePaymentProvider>();
        services.AddSingleton<CheckoutService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/checkout", async (
            HttpRequest httpRequest,
            CheckoutRequest body,
            CheckoutService service,
            CancellationToken ct) =>
        {
            if (!httpRequest.Headers.TryGetValue(IdempotencyHeader, out var keys)
                || string.IsNullOrWhiteSpace(keys.ToString()))
            {
                return Results.BadRequest(new ProblemDto(
                    "missing_idempotency_key",
                    $"{IdempotencyHeader} header is required."));
            }

            var outcome = await service.CheckoutAsync(body, keys.ToString(), ct);
            return outcome switch
            {
                CheckoutOutcome.ConfirmedResult c => Results.Created($"/orders/{c.Response.OrderId}", c.Response),
                CheckoutOutcome.ReplayedResult r => Results.Created($"/orders/{r.Response.OrderId}", r.Response),
                CheckoutOutcome.DeclinedOutcome d => Results.Json(d.Problem, statusCode: StatusCodes.Status402PaymentRequired),
                CheckoutOutcome.InvalidResult i => Results.BadRequest(i.Problem),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        })
        .WithName("Checkout")
        .WithTags("Checkout");

        endpoints.MapGet("/orders/{id:guid}", (Guid id, IOrderStore store) =>
        {
            var order = store.Get(id);
            if (order is null) return Results.NotFound();

            var dto = new OrderDto(
                order.Id,
                order.CustomerEmail,
                order.Status.ToString(),
                order.Currency,
                order.TotalCents,
                order.Items.Select(i => new OrderItemDto(i.Sku, i.Quantity, i.UnitPriceCents)).ToList());
            return Results.Ok(dto);
        })
        .WithName("GetOrder")
        .WithTags("Checkout");

        return endpoints;
    }
}
