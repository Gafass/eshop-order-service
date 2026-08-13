using MongoDB.Bson.Serialization.Attributes;

namespace OrderService.Api.Domain;

public enum OrderStatus { Pending, Confirmed, Cancelled }

public sealed class Order
{
    [BsonId]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string CustomerId { get; init; }
    public string? BasketId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public required List<OrderItem> Items { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public required string IdempotencyKey { get; init; }

    public bool ChangeStatus(OrderStatus next)
    {
        if (Status != OrderStatus.Pending || next == OrderStatus.Pending) return false;
        Status = next;
        return true;
    }
}

public sealed record OrderItem(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
