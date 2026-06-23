using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TravelAssistant.Api.Checkout;
using Xunit;

namespace TravelAssistant.Api.Tests.Checkout;

[Trait("Category", "Integration")]
public class CheckoutEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage NewCheckoutPost(CheckoutRequest body, string idempotencyKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/checkout") { Content = JsonContent.Create(body) };
        req.Headers.Add(CheckoutEndpoints.IdempotencyHeader, idempotencyKey);
        return req;
    }

    [Fact]
    public async Task Post_checkout_without_idempotency_header_returns_400()
    {
        var client = _factory.CreateClient();
        var body = new CheckoutRequest("a@b.c",
            new[] { new CartItemDto("SKU", 1, 100) }, "tok", "USD");
        var resp = await client.PostAsJsonAsync("/checkout", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_checkout_with_valid_cart_returns_201_and_get_order_returns_it()
    {
        var client = _factory.CreateClient();
        var body = new CheckoutRequest("ada@example.com",
            new[] { new CartItemDto("SKU-1", 2, 1_500) }, "tok_ok", "USD");

        var resp = await client.SendAsync(NewCheckoutPost(body, Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var checkout = await resp.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(checkout);
        Assert.Equal(3_000, checkout!.TotalCents);

        var getResp = await client.GetAsync($"/orders/{checkout.OrderId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var order = await getResp.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("ada@example.com", order!.CustomerEmail);
        Assert.Equal("Confirmed", order.Status);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task Repeating_same_idempotency_key_returns_same_order_id()
    {
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString("N");
        var body = new CheckoutRequest("grace@example.com",
            new[] { new CartItemDto("SKU-9", 1, 999) }, "tok_ok", "USD");

        var first = await client.SendAsync(NewCheckoutPost(body, key));
        var second = await client.SendAsync(NewCheckoutPost(body, key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstDto = await first.Content.ReadFromJsonAsync<CheckoutResponse>();
        var secondDto = await second.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal(firstDto!.OrderId, secondDto!.OrderId);
    }

    [Fact]
    public async Task Get_order_returns_404_for_unknown_id()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
