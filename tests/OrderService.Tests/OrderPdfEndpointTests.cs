using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OrderService.Api.Application;
using OrderService.Api.Domain;

namespace OrderService.Tests;

public sealed class OrderPdfEndpointTests : IClassFixture<OrderPdfApiFactory>
{
    private readonly HttpClient _client;

    public OrderPdfEndpointTests(OrderPdfApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Existing_order_returns_inline_pdf()
    {
        using var response = await _client.GetAsync($"/api/orders/{OrderPdfApiFactory.ExistingOrderId}/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"orden-{OrderPdfApiFactory.ExistingOrderId}.pdf",
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public async Task Missing_order_returns_404()
    {
        using var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid():N}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_order_id_returns_400()
    {
        using var response = await _client.GetAsync("/api/orders/no-es-un-folio/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_order_returns_basket_id()
    {
        using var response = await _client.GetAsync($"/api/orders/{OrderPdfApiFactory.ExistingOrderId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("codex-render-smoke-20260812", json.RootElement.GetProperty("basketId").GetString());
    }

    [Fact]
    public async Task Get_customer_orders_preserves_basket_id()
    {
        using var response = await _client.GetAsync("/api/orders/customer/codex-render-smoke-20260812");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("codex-render-smoke-20260812", json.RootElement[0].GetProperty("basketId").GetString());
    }
}

public sealed class OrderPdfApiFactory : WebApplicationFactory<Program>
{
    public const string ExistingOrderId = "11111111111111111111111111111111";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrderRepository>();
            services.AddSingleton<IOrderRepository>(new EndpointFakeRepository());
        });
    }

    private sealed class EndpointFakeRepository : IOrderRepository
    {
        private readonly Order _order = new()
        {
            Id = ExistingOrderId,
            CustomerId = "codex-render-smoke-20260812",
            BasketId = "codex-render-smoke-20260812",
            CreatedAt = new DateTime(2026, 8, 13, 6, 30, 0, DateTimeKind.Utc),
            Items =
            [
                new("p1", "Teclado", 1, 3999m, 3999m),
                new("p2", "Mouse", 2, 250m, 500m)
            ],
            Subtotal = 4499m,
            Tax = 719.84m,
            Total = 5218.84m,
            IdempotencyKey = "endpoint-pdf-test"
        };

        public Task<Order?> GetByIdAsync(string id, CancellationToken ct) =>
            Task.FromResult<Order?>(id == _order.Id ? _order : null);
        public Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Order?>(null);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Order>>(customerId == _order.CustomerId ? [_order] : []);
        public Task<Order> CreateAsync(Order order, CancellationToken ct) => Task.FromResult(order);
        public Task UpdateStatusAsync(Order order, CancellationToken ct) => Task.CompletedTask;
        public Task CheckHealthAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
