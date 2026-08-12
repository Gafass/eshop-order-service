using OrderService.Api.Application;
using OrderService.Api.Endpoints;
using OrderService.Api.Infrastructure;
using MongoDB.Driver;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.AddSingleton<IOrderRepository, MongoOrderRepository>();
builder.Services.AddScoped<OrderApplicationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IBasketClient, BasketClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:BasketUrl"]!));
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:CatalogUrl"]!));

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, title) = exception switch
    {
        BusinessRuleException => (400, exception.Message),
        ResourceNotFoundException => (404, exception.Message),
        BadHttpRequestException => (400, "La solicitud JSON no es válida."),
        HttpRequestException => (503, "Un servicio requerido no está disponible."),
        MongoException => (503, "MongoDB no está disponible o rechazó la conexión."),
        TimeoutException => (503, "MongoDB no respondió dentro del tiempo esperado."),
        InvalidOperationException when exception.Message.Contains("MongoDB__ConnectionString") =>
            (503, "MongoDB no está configurado para este proceso."),
        _ => (500, "No fue posible completar la operación.")
    };
    if (status >= 500)
        app.Logger.LogError("Order request failed with {ExceptionType} and status {StatusCode}", exception?.GetType().Name, status);
    context.Response.StatusCode = status;
    await Results.Problem(title: title, statusCode: status).ExecuteAsync(context);
}));
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/live", () => Results.Ok(new { status = "Healthy", service = "OrderService" }));
app.MapGet("/health", async (IOrderRepository repository, CancellationToken cancellationToken) =>
{
    await repository.CheckHealthAsync(cancellationToken);
    return Results.Ok(new { status = "Healthy", service = "OrderService", mongodb = "Connected" });
});
app.MapOrderEndpoints();
app.Run();

public partial class Program;
