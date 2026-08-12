using OrderService.Api.Domain;

namespace OrderService.Api.Application;

public sealed record CreateOrderRequest(string CustomerId, string? BasketId);
public sealed record ChangeOrderStatusRequest(string Status);
public sealed record BasketResponse(ShoppingCart Cart);
public sealed record ShoppingCart(string UserName, List<BasketItem> Items);
public sealed record BasketItem(int Quantity, decimal Price, string ProductId, string ProductName);
public sealed record ProductPage(List<CatalogProduct> Data);
public sealed record CatalogProduct(string Id, string Name, decimal Price);

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken cancellationToken);
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken);
    Task UpdateStatusAsync(Order order, CancellationToken cancellationToken);
    Task CheckHealthAsync(CancellationToken cancellationToken);
}

public interface IBasketClient
{
    Task<ShoppingCart?> GetAsync(string basketId, CancellationToken cancellationToken);
}

public interface ICatalogClient
{
    Task<IReadOnlyDictionary<string, CatalogProduct>> GetProductsAsync(CancellationToken cancellationToken);
}

public sealed class BusinessRuleException(string message) : Exception(message);
public sealed class ResourceNotFoundException(string message) : Exception(message);
