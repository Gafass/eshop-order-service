using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Application;

namespace OrderService.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", async ([FromBody] CreateOrderRequest request, HttpRequest httpRequest,
            OrderApplicationService service, CancellationToken cancellationToken) =>
        {
            var key = httpRequest.Headers["Idempotency-Key"].ToString();
            var result = await service.CreateAsync(request, key, cancellationToken);
            return result.Created
                ? Results.Created($"/api/orders/{result.Order.Id}", result.Order)
                : Results.Ok(result.Order);
        })
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithName("CreateOrder");

        group.MapGet("/{id}", async (string id, OrderApplicationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)))
            .Produces(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetOrder");

        group.MapGet("/customer/{customerId}", async (string customerId, OrderApplicationService service, CancellationToken ct) =>
            Results.Ok(await service.GetByCustomerAsync(customerId, ct)))
            .Produces(StatusCodes.Status200OK).WithName("GetCustomerOrders");

        group.MapPatch("/{id}/status", async (string id, ChangeOrderStatusRequest request,
            OrderApplicationService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeStatusAsync(id, request, ct)))
            .Produces(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).WithName("ChangeOrderStatus");

        return group;
    }
}
