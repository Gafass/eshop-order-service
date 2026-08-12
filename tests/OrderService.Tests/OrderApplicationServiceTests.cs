using OrderService.Api.Application;
using OrderService.Api.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace OrderService.Tests;

public sealed class OrderApplicationServiceTests
{
    [Fact]
    public async Task Create_calculates_totals_and_persists_order()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, [new BasketItem(2, 100m, "p1", "Teclado")]);

        var (order, created) = await service.CreateAsync(new("rafa", null), "key-1", default);

        Assert.True(created);
        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(32m, order.Tax);
        Assert.Equal(232m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Repeated_idempotency_key_returns_same_order()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, [new BasketItem(1, 100m, "p1", "Teclado")]);

        var first = await service.CreateAsync(new("rafa", null), "same-key", default);
        var second = await service.CreateAsync(new("rafa", null), "same-key", default);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Order.Id, second.Order.Id);
        Assert.Single(repository.Orders);
    }

    [Fact]
    public async Task Empty_basket_is_rejected()
    {
        var service = CreateService(new FakeRepository(), []);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new("rafa", null), "key-empty", default));
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_confirmed()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, [new BasketItem(1, 100m, "p1", "Teclado")]);
        var created = await service.CreateAsync(new("rafa", null), "key-status", default);

        await service.ChangeStatusAsync(created.Order.Id, new("Cancelled"), default);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangeStatusAsync(created.Order.Id, new("Confirmed"), default));
    }

    private static OrderApplicationService CreateService(FakeRepository repository, List<BasketItem> items) =>
        new(repository, new FakeBasketClient(new ShoppingCart("rafa", items)),
            new FakeCatalogClient(new CatalogProduct("p1", "Teclado", 100m)), TimeProvider.System,
            NullLogger<OrderApplicationService>.Instance);

    private sealed class FakeBasketClient(ShoppingCart cart) : IBasketClient
    {
        public Task<ShoppingCart?> GetAsync(string basketId, CancellationToken cancellationToken) => Task.FromResult<ShoppingCart?>(cart);
    }

    private sealed class FakeCatalogClient(params CatalogProduct[] products) : ICatalogClient
    {
        public Task<IReadOnlyDictionary<string, CatalogProduct>> GetProductsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, CatalogProduct>>(products.ToDictionary(p => p.Id));
    }

    private sealed class FakeRepository : IOrderRepository
    {
        public List<Order> Orders { get; } = [];
        public Task<Order?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Orders.SingleOrDefault(o => o.Id == id));
        public Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Orders.SingleOrDefault(o => o.IdempotencyKey == key));
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Order>>(Orders.Where(o => o.CustomerId == customerId).ToList());
        public Task<Order> CreateAsync(Order order, CancellationToken ct) { Orders.Add(order); return Task.FromResult(order); }
        public Task UpdateStatusAsync(Order order, CancellationToken ct) => Task.CompletedTask;
        public Task CheckHealthAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
