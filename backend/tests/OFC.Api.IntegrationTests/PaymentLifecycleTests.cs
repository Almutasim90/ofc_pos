using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Payments;
using Xunit;

namespace OFC.Api.IntegrationTests;

// The SRS's electronic payment lifecycle (Pending -> Authorized -> Captured) previously had no
// endpoint that ever produced an Authorized payment — every capture jumped straight to Captured.
// This exercises the real two-phase flow end to end: authorize (order stays unpaid), capture (order
// becomes Paid), then reverse (a correction distinct from a customer refund).
public class PaymentLifecycleTests
{
    private static async Task<(System.Net.Http.HttpClient Client, Guid BranchId, Guid ProductId, Guid ChannelId, Guid CardMethodId)> Seed(ApiFactory factory)
    {
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "payments-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "payments-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var branches = await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>();
        var branchId = branches[0].GetProperty("id").GetGuid();

        var channelResponse = await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "POS", nameAr = "الصالة", nameEn = "Dine-in", isActive = true });
        var channelId = (await channelResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "مشروبات", nameEn = "Drinks", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null });
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var productResponse = await client.PostAsJsonAsync("/api/v1/products", new { sku = "TEST-COLA", barcode = (string?)null, nameAr = "كولا", nameEn = "Cola", descriptionAr = (string?)null, descriptionEn = (string?)null, categoryId, type = "Simple", taxCategoryId = (Guid?)null, preparationStationId = (Guid?)null, basePrice = 5m, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = true } } });
        var productId = (await productResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var methodResponse = await client.PostAsJsonAsync("/api/v1/payment-methods", new { branchId, code = "CARD", nameAr = "بطاقة", nameEn = "Card", kind = "Visa", sortOrder = 0 });
        var cardMethodId = (await methodResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return (client, branchId, productId, channelId, cardMethodId);
    }

    private static async Task<Guid> CreatePendingOrder(System.Net.Http.HttpClient client, Guid branchId, Guid channelId, Guid productId)
    {
        var orderResponse = await client.PostAsJsonAsync("/api/v1/orders", new { branchId, salesChannelId = channelId, clientRequestId = Guid.NewGuid(), source = "Pos", note = (string?)null, lines = new[] { new { productId, quantity = 1, note = (string?)null, selections = Array.Empty<object>() } } });
        Assert.True(orderResponse.StatusCode == HttpStatusCode.Created, await orderResponse.Content.ReadAsStringAsync());
        var order = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();

        var statusResponse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Pending", note = (string?)null });
        Assert.True(statusResponse.StatusCode == HttpStatusCode.OK, await statusResponse.Content.ReadAsStringAsync());
        return orderId;
    }

    [Fact]
    public async Task Authorizing_an_electronic_payment_does_not_mark_the_order_paid()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, cardMethodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = cardMethodId, amount = 5m, tenderedAmount = 5m, status = "Authorized", providerReference = "AUTH-1" } } });
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);
        var payBody = await payResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authorized", payBody.GetProperty("payments")[0].GetProperty("status").GetString());

        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        Assert.Empty(await db.FinancialTransactions.Where(x => x.OrderId == orderId).ToListAsync());
    }

    [Fact]
    public async Task Capturing_the_last_authorized_payment_marks_the_order_paid()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, cardMethodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = cardMethodId, amount = 5m, tenderedAmount = 5m, status = "Authorized", providerReference = "AUTH-2" } } });
        var paymentId = (await payResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments")[0].GetProperty("id").GetGuid();

        var captureResponse = await client.PostAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/capture", null);
        Assert.Equal(HttpStatusCode.OK, captureResponse.StatusCode);
        var captured = await captureResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Captured", captured.GetProperty("payments")[0].GetProperty("status").GetString());

        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        var duplicate = await client.PostAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/capture", null);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        Assert.Single(await db.FinancialTransactions.Where(x => x.PaymentId == paymentId).ToListAsync());
    }

    [Fact]
    public async Task A_captured_payment_can_be_reversed_as_a_correction()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, cardMethodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);

        var payResponse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = cardMethodId, amount = 5m, tenderedAmount = 5m, status = "Captured", providerReference = "CAP-1" } } });
        var paymentId = (await payResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments")[0].GetProperty("id").GetGuid();

        var reverseResponse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/reverse", new { reason = "Captured against the wrong card" });
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversed = await reverseResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reversed", reversed.GetProperty("payments")[0].GetProperty("status").GetString());

        // Reversing again must fail closed — a payment is reversed at most once.
        var secondReverse = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/reverse", new { reason = "again" });
        Assert.Equal(HttpStatusCode.BadRequest, secondReverse.StatusCode);
        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var ledger = await db.FinancialTransactions.Where(x => x.PaymentId == paymentId).ToListAsync();
        var sale = Assert.Single(ledger, x => x.Type == FinancialTransactionType.Sale);
        var reversal = Assert.Single(ledger, x => x.Type == FinancialTransactionType.Reversal);
        Assert.Equal(sale.Id, reversal.ReversalReferenceId);
        Assert.Equal(sale.Amount, reversal.Amount);
    }

    [Fact]
    public async Task Split_authorizations_are_paid_only_after_both_captures_and_can_be_corrected()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, methodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);
        var request = new { payments = new[] {
            new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 2m, tenderedAmount = 2m, status = "Authorized" },
            new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 3m, tenderedAmount = 3m, status = "Authorized" }
        } };
        var response = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var payments = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments");
        var firstId = payments[0].GetProperty("id").GetGuid();
        var secondId = payments[1].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", request)).StatusCode);
        var newAttempt = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 5m, tenderedAmount = 5m, status = "Captured" } } });
        Assert.Equal(HttpStatusCode.BadRequest, newAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/orders/{orderId}/payments/{firstId}/capture", null)).StatusCode);
        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/orders/{orderId}/payments/{secondId}/capture", null)).StatusCode);
        order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments/{firstId}/reverse", new { reason = "Wrong card" })).StatusCode);
        var replacement = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 2m, tenderedAmount = 2m, status = "Captured" } } });
        Assert.True(replacement.IsSuccessStatusCode, await replacement.Content.ReadAsStringAsync());
        order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope();
        var ledger = await scope.ServiceProvider.GetRequiredService<OFCDbContext>().FinancialTransactions.Where(x => x.OrderId == orderId).ToListAsync();
        Assert.Equal(5m, ledger.Sum(x => x.Type == FinancialTransactionType.Sale ? x.Amount : -x.Amount));
    }

    [Fact]
    public async Task Cancelled_order_cannot_capture_an_authorization()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, methodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);
        var response = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 5m, tenderedAmount = 5m, status = "Authorized" } } });
        var paymentId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Cancelled" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/capture", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<OFCDbContext>().FinancialTransactions.Where(x => x.OrderId == orderId).ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Reversal_requires_a_reason(string? reason)
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, methodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);
        var response = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 5m, tenderedAmount = 5m, status = "Captured" } } });
        var paymentId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/reverse", new { reason })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var ledger = await scope.ServiceProvider.GetRequiredService<OFCDbContext>().FinancialTransactions.Where(x => x.PaymentId == paymentId).ToListAsync();
        Assert.Equal(FinancialTransactionType.Sale, Assert.Single(ledger).Type);
    }
}
