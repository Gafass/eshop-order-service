using OrderService.Api.Domain;

namespace OrderService.Api.Application;

public sealed class OrderApplicationService(
    IOrderRepository repository,
    IBasketClient basketClient,
    ICatalogClient catalogClient,
    IOrderPdfGenerator pdfGenerator,
    TimeProvider timeProvider,
    ILogger<OrderApplicationService> logger)
{
    private const decimal TaxRate = 0.16m;

    public async Task<(Order Order, bool Created)> CreateAsync(
        CreateOrderRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new BusinessRuleException("CustomerId es obligatorio.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BusinessRuleException("El header Idempotency-Key es obligatorio.");
        if (idempotencyKey.Length > 128)
            throw new BusinessRuleException("Idempotency-Key no puede exceder 128 caracteres.");

        var existing = await repository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Returning existing order {OrderId} for repeated idempotency key", existing.Id);
            return (existing, false);
        }

        var customerId = request.CustomerId.Trim();
        var basketId = (string.IsNullOrWhiteSpace(request.BasketId) ? customerId : request.BasketId).Trim();
        logger.LogInformation("Retrieving basket {BasketId}", basketId);
        var basket = await basketClient.GetAsync(basketId, cancellationToken);
        if (basket is null || basket.Items.Count == 0)
        {
            logger.LogWarning("Empty or missing basket {BasketId}", basketId);
            throw new BusinessRuleException("El Basket está vacío o no existe.");
        }
        logger.LogInformation("Basket {BasketId} retrieved with {ItemCount} items", basketId, basket.Items.Count);
        if (!string.Equals(basket.UserName, customerId, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("El Basket no pertenece al cliente indicado.");

        logger.LogInformation("Calling catalog and validating basket items");
        var catalog = await catalogClient.GetProductsAsync(cancellationToken);
        var items = new List<OrderItem>();
        foreach (var item in basket.Items)
        {
            if (item.Quantity <= 0 || item.Price <= 0 || string.IsNullOrWhiteSpace(item.ProductId))
                throw new BusinessRuleException("El Basket contiene cantidades, precios o identificadores inválidos.");
            if (!catalog.TryGetValue(item.ProductId, out var product))
                throw new BusinessRuleException($"El producto {item.ProductId} no existe en Catálogo.");
            if (product.Price != item.Price)
                throw new BusinessRuleException($"El precio de {product.Name} cambió; actualiza el Basket.");

            items.Add(new OrderItem(product.Id, product.Name, item.Quantity, item.Price,
                decimal.Round(item.Price * item.Quantity, 2)));
        }

        var subtotal = items.Sum(item => item.LineTotal);
        var tax = decimal.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
        var order = new Order
        {
            CustomerId = customerId,
            BasketId = basketId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Items = items,
            Subtotal = subtotal,
            Tax = tax,
            Total = subtotal + tax,
            IdempotencyKey = idempotencyKey.Trim()
        };
        logger.LogInformation("Creating MongoDB document for customer {CustomerId}", order.CustomerId);
        var saved = await repository.CreateAsync(order, cancellationToken);
        logger.LogInformation("Order {OrderId} created with total {Total}", saved.Id, saved.Total);
        return (saved, true);
    }

    public async Task<Order> GetAsync(string id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new ResourceNotFoundException("Orden no encontrada.");

    public async Task<byte[]> GetPdfAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParseExact(id, "N", out _))
            throw new BusinessRuleException("El folio de la orden no es válido.");

        logger.LogInformation("Generating PDF for Order {OrderId}", id);
        var order = await GetAsync(id, cancellationToken);

        try
        {
            var pdf = pdfGenerator.Generate(order);
            logger.LogInformation("PDF generated for Order {OrderId}", id);
            return pdf;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to generate PDF for Order {OrderId}", id);
            throw;
        }
    }

    public Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new BusinessRuleException("CustomerId es obligatorio.");
        return repository.GetByCustomerAsync(customerId, cancellationToken);
    }

    public async Task<Order> ChangeStatusAsync(string id, ChangeOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await GetAsync(id, cancellationToken);
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var next))
            throw new BusinessRuleException("Estado inválido. Usa Pending, Confirmed o Cancelled.");
        if (!order.ChangeStatus(next))
            throw new BusinessRuleException($"La transición {order.Status} → {next} no está permitida.");
        await repository.UpdateStatusAsync(order, cancellationToken);
        return order;
    }
}
