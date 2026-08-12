using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OrderService.Api.Application;
using OrderService.Api.Domain;

namespace OrderService.Api.Infrastructure;

public sealed class MongoOptions
{
    public const string SectionName = "MongoDB";
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "eshop-orders";
    public string CollectionName { get; set; } = "orders";
}

public sealed class MongoOrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _orders;
    private readonly IMongoDatabase _database;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoOrderRepository(IOptions<MongoOptions> options)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.ConnectionString))
            throw new InvalidOperationException("Configura MongoDB__ConnectionString mediante una variable de entorno.");
        var client = new MongoClient(value.ConnectionString);
        _database = client.GetDatabase(value.DatabaseName);
        _orders = _database.GetCollection<Order>(value.CollectionName);
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady) return;
        var idempotency = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(order => order.IdempotencyKey),
            new CreateIndexOptions { Unique = true, Name = "ux_idempotency_key" });
        var customer = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(order => order.CustomerId).Descending(order => order.CreatedAt),
            new CreateIndexOptions { Name = "ix_customer_created" });
            await _orders.Indexes.CreateManyAsync([idempotency, customer], cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public async Task<Order?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _orders.Find(order => order.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _orders.Find(order => order.IdempotencyKey == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _orders.Find(order => order.CustomerId == customerId)
            .SortByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        try
        {
            await _orders.InsertOneAsync(order, cancellationToken: cancellationToken);
            return order;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetByIdempotencyKeyAsync(order.IdempotencyKey, cancellationToken);
            if (existing is not null) return existing;
            throw;
        }
    }

    public async Task UpdateStatusAsync(Order order, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        await _orders.ReplaceOneAsync(saved => saved.Id == order.Id, order, cancellationToken: cancellationToken);
    }

    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        await EnsureIndexesAsync(cancellationToken);
    }
}
