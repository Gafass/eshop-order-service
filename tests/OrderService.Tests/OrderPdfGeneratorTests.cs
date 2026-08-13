using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Api.Application;
using OrderService.Api.Domain;
using OrderService.Api.Infrastructure;
using QuestPDF.Infrastructure;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace OrderService.Tests;

public sealed class OrderPdfGeneratorTests
{
    static OrderPdfGeneratorTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Theory]
    [InlineData("rafa")]
    [InlineData("codex-render-smoke-20260812")]
    public void Generate_returns_pdf_for_each_customer(string customerId)
    {
        var pdf = new OrderPdfGenerator().Generate(CreateOrder(customerId));

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Generate_supports_multiple_items()
    {
        var order = CreateOrder("rafa", [
            new("p1", "Teclado", 1, 100m, 100m),
            new("p2", "Mouse", 2, 50m, 100m),
            new("p3", "Monitor", 1, 300m, 300m)
        ]);

        var pdf = new OrderPdfGenerator().Generate(order);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public async Task Application_returns_404_for_missing_order_pdf()
    {
        var service = CreateService(new PdfFakeRepository());

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            service.GetPdfAsync(Guid.NewGuid().ToString("N"), default));
    }

    [Fact]
    public async Task Application_generates_pdf_for_existing_order()
    {
        var repository = new PdfFakeRepository();
        var order = CreateOrder("codex-render-smoke-20260812");
        repository.Orders.Add(order);
        var service = CreateService(repository);

        var pdf = await service.GetPdfAsync(order.Id, default);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void Historical_bson_without_basket_id_deserializes_and_generates_pdf()
    {
        var source = CreateOrder("rafa").ToBsonDocument();
        source.Remove(nameof(Order.BasketId));

        var historicalOrder = BsonSerializer.Deserialize<Order>(source);
        var pdf = new OrderPdfGenerator().Generate(historicalOrder);

        Assert.Null(historicalOrder.BasketId);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 1000);
    }

    private static OrderApplicationService CreateService(PdfFakeRepository repository) =>
        new(repository, new EmptyBasketClient(), new EmptyCatalogClient(), new OrderPdfGenerator(),
            TimeProvider.System, NullLogger<OrderApplicationService>.Instance);

    private static Order CreateOrder(string customerId, List<OrderItem>? items = null)
    {
        items ??= [new("p1", "Teclado Mecánico", 1, 3999m, 3999m)];
        var subtotal = items.Sum(item => item.LineTotal);
        var tax = decimal.Round(subtotal * 0.16m, 2);
        return new Order
        {
            CustomerId = customerId,
            BasketId = customerId,
            CreatedAt = new DateTime(2026, 8, 13, 6, 30, 0, DateTimeKind.Utc),
            Items = items,
            Subtotal = subtotal,
            Tax = tax,
            Total = subtotal + tax,
            IdempotencyKey = Guid.NewGuid().ToString()
        };
    }

    private sealed class EmptyBasketClient : IBasketClient
    {
        public Task<ShoppingCart?> GetAsync(string basketId, CancellationToken cancellationToken) =>
            Task.FromResult<ShoppingCart?>(null);
    }

    private sealed class EmptyCatalogClient : ICatalogClient
    {
        public Task<IReadOnlyDictionary<string, CatalogProduct>> GetProductsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, CatalogProduct>>(new Dictionary<string, CatalogProduct>());
    }

    private sealed class PdfFakeRepository : IOrderRepository
    {
        public List<Order> Orders { get; } = [];
        public Task<Order?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Orders.SingleOrDefault(o => o.Id == id));
        public Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult<Order?>(null);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task<Order> CreateAsync(Order order, CancellationToken ct) => Task.FromResult(order);
        public Task UpdateStatusAsync(Order order, CancellationToken ct) => Task.CompletedTask;
        public Task CheckHealthAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
