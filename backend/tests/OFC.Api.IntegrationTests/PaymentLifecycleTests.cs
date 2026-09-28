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
    [Theory]
    [InlineData("Admin")]
    [InlineData("Branch Manager")]
    public async Task Manager_can_sell_electronic_orders_after_login(string roleName)
    {
        using var factory = new ApiFactory();
        var (admin, branchId, productId, _, _) = await Seed(factory);
        var roles = await admin.GetFromJsonAsync<JsonElement>("/api/v1/roles");
        var roleId = roles.EnumerateArray().Single(x => x.GetProperty("name").GetString() == roleName).GetProperty("id").GetGuid();
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { username = "selling-manager", displayName = roleName, password = "password1234", roleIds = new[] { roleId }, branchIds = new[] { branchId } });
        created.EnsureSuccessStatusCode();
        var channelResponse = await admin.PostAsJsonAsync("/api/v1/sales-channels", new { code = "TALABAT", nameAr = "طلبات", nameEn = "Talabat", kind = "Electronic", isActive = true });
        channelResponse.EnsureSuccessStatusCode();
        var channelId = (await channelResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var methodResponse = await admin.PostAsJsonAsync("/api/v1/payment-methods", new { branchId, code = "EXTERNAL", nameAr = "دفع خارجي", nameEn = "External payment", kind = "External", sortOrder = 1 });
        methodResponse.EnsureSuccessStatusCode();
        var methodId = (await methodResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (manager, _) = await factory.AuthenticatedClientAsync("selling-manager", "password1234");
        (await manager.GetAsync("/api/v1/pos/context")).EnsureSuccessStatusCode();
        (await manager.GetAsync($"/api/v1/pos/catalog?branchId={branchId}&salesChannelId={channelId}")).EnsureSuccessStatusCode();
        (await manager.GetAsync($"/api/v1/payment-methods?branchId={branchId}")).EnsureSuccessStatusCode();
        var orderId = await CreatePendingOrder(manager, branchId, channelId, productId);
        var payment = await manager.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 5m, tenderedAmount = 5m, status = "Captured", providerReference = "TALABAT-TEST" } } });
        Assert.True(payment.IsSuccessStatusCode, await payment.Content.ReadAsStringAsync());
        var order = await manager.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Paid", order.GetProperty("status").GetString());
    }

    // Pay later (e.g. a VIP guest): the order goes to the kitchen unpaid, is not a sale until it is paid,
    // stays in "current orders", and paying it keeps the kitchen's progress.
    [Fact]
    public async Task A_pay_later_order_goes_to_the_kitchen_unpaid_and_is_paid_afterwards()
    {
        using var factory = new ApiFactory();
        var (client, branchId, productId, channelId, cardMethodId) = await Seed(factory);
        var orderId = await CreatePendingOrder(client, branchId, channelId, productId);
        var dispatch = await client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { branchId, orderId, clientDispatchId = Guid.NewGuid(), orderNumber = (string?)null, note = (string?)null, targetMinutes = (int?)null });
        Assert.True(dispatch.StatusCode == HttpStatusCode.Created, await dispatch.Content.ReadAsStringAsync());
        var ticketId = (await dispatch.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        (await client.PostAsync($"/api/v1/kitchen/tickets/{ticketId}/ready", null)).EnsureSuccessStatusCode();

        var order = await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Ready", order.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, order.GetProperty("paidAt").ValueKind);
        var sales = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sales?branchId={branchId}");
        Assert.Equal(0, sales.GetProperty("summary").GetProperty("orderCount").GetInt32());
        var current = await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders?branchId={branchId}&scope=shift");
        Assert.Contains(current.EnumerateArray(), x => x.GetProperty("id").GetGuid() == orderId);

        var paid = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = cardMethodId, amount = 5m, tenderedAmount = 5m, status = "Captured", providerReference = "POS-LATER" } } });
        Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        order = await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("Ready", order.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, order.GetProperty("paidAt").ValueKind);
        sales = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/sales?branchId={branchId}");
        Assert.Equal(1, sales.GetProperty("summary").GetProperty("orderCount").GetInt32());
        var again = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = cardMethodId, amount = 5m, tenderedAmount = 5m, status = "Captured", providerReference = "POS-TWICE" } } });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        current = await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders?branchId={branchId}&scope=shift");
        Assert.DoesNotContain(current.EnumerateArray(), x => x.GetProperty("id").GetGuid() == orderId);
    }

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
        var replacement = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 2m, tenderedAmount = 2m, status = "Captured", providerReference = "CAP-REPLACEMENT" } } });
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
        var response = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments", new { payments = new[] { new { clientRequestId = Guid.NewGuid(), paymentMethodId = methodId, amount = 5m, tenderedAmount = 5m, status = "Captured", providerReference = "CAP-REASON-TEST" } } });
        var paymentId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("payments")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/payments/{paymentId}/reverse", new { reason })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var ledger = await scope.ServiceProvider.GetRequiredService<OFCDbContext>().FinancialTransactions.Where(x => x.PaymentId == paymentId).ToListAsync();
        Assert.Equal(FinancialTransactionType.Sale, Assert.Single(ledger).Type);
    }
}
