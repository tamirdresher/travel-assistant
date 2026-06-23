using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TravelAssistant.Api.Tests.Checkout;

// QA regression tests for checkout idempotency bugs found in commit f835801.
// Filed: https://github.com/tamirdresher/travel-assistant/issues/46  (BUG-1 body-blind cache)
// Filed: https://github.com/tamirdresher/travel-assistant/issues/47  (BUG-2 status-code laundering)
//
// These tests pin the *correct* contract per RFC draft-ietf-httpapi-idempotency-key-header §2.7.
// They are marked Skip until the checkout module is merged into src/TravelAssistant.Api.
// Once the checkout endpoints + IdempotencyStore land, remove the Skip and both bugs must be fixed
// for the suite to pass.
//
// Author: quality-testing-squad (Hockney)
public sealed class IdempotencyRegressionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Skip = "Activates when checkout module merges; tracks #46 + #47.";
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IdempotencyRegressionTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _client = factory.CreateClient();
    }

    // ---------- BUG-1: body-blind idempotency cache ----------

    [Fact(Skip = Skip)]
    public async Task BUG1_Replay_With_Same_Key_But_Different_Body_Returns_422()
    {
        var key = Guid.NewGuid().ToString();
        var sessionA = await CreateConfirmableSession(items: new[] { ("sku-A", 1, 5000) });
        var sessionB = await CreateConfirmableSession(items: new[] { ("sku-B", 1, 50) });

        var first = await Confirm(key, sessionA, paymentToken: "tok_visa_ok");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same key, *different body* → MUST be 422 per RFC.
        var second = await Confirm(key, sessionB, paymentToken: "tok_visa_ok");
        Assert.Equal((HttpStatusCode)422, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("idempotency_key_mismatch", problem.GetProperty("error").GetString());
    }

    [Fact(Skip = Skip)]
    public async Task BUG1_Replay_With_Same_Key_And_Identical_Body_Returns_Cached_Response()
    {
        var key = Guid.NewGuid().ToString();
        var session = await CreateConfirmableSession(items: new[] { ("sku-A", 1, 5000) });

        var first = await Confirm(key, session, paymentToken: "tok_visa_ok");
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await Confirm(key, session, paymentToken: "tok_visa_ok");
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(firstBody, secondBody);
    }

    // ---------- BUG-2: failure status code laundered to 200 ----------

    [Fact(Skip = Skip)]
    public async Task BUG2_Failed_Payment_Replay_Preserves_402_Status()
    {
        var key = Guid.NewGuid().ToString();
        var session = await CreateConfirmableSession(items: new[] { ("sku-A", 1, 5000) });

        // First call: card declined → expect 402.
        var first = await Confirm(key, session, paymentToken: "tok_chargeDeclined");
        Assert.Equal((HttpStatusCode)402, first.StatusCode);

        // Replay must also be 402, NOT 200.
        var second = await Confirm(key, session, paymentToken: "tok_chargeDeclined");
        Assert.Equal((HttpStatusCode)402, second.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, secondBody);
    }

    [Fact(Skip = Skip)]
    public async Task BUG2_Success_Replay_Preserves_200_Status()
    {
        var key = Guid.NewGuid().ToString();
        var session = await CreateConfirmableSession(items: new[] { ("sku-A", 1, 5000) });

        var first = await Confirm(key, session, paymentToken: "tok_visa_ok");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Confirm(key, session, paymentToken: "tok_visa_ok");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // ---------- helpers ----------

    private async Task<string> CreateConfirmableSession((string sku, int qty, int amountMinor)[] items)
    {
        // POST /session → Details
        var sessionResp = await _client.PostAsJsonAsync("/api/checkout/session", new
        {
            items = items.Select(i => new { sku = i.sku, qty = i.qty, amountCents = i.amountMinor, currency = "USD" })
        });
        sessionResp.EnsureSuccessStatusCode();
        var sessionId = (await sessionResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;

        // POST /details → Payment
        var detailsResp = await _client.PostAsJsonAsync("/api/checkout/details", new
        {
            sessionId,
            traveler = new { fullName = "Test Traveler", email = "qa@example.test" }
        });
        detailsResp.EnsureSuccessStatusCode();
        return sessionId;
    }

    private async Task<HttpResponseMessage> Confirm(string idemKey, string sessionId, string paymentToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/confirm")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { sessionId, paymentToken }, Json),
                Encoding.UTF8,
                "application/json")
        };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
        return await _client.SendAsync(req);
    }
}
