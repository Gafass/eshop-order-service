using System.Net;
using System.Net.Http.Json;
using OrderService.Api.Application;

namespace OrderService.Api.Infrastructure;

public sealed class BasketClient(HttpClient httpClient, ILogger<BasketClient> logger) : IBasketClient
{
    public async Task<ShoppingCart?> GetAsync(string basketId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"basket/{Uri.EscapeDataString(basketId)}", cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            logger.LogWarning("Basket service returned status {StatusCode} for basket {BasketId}", (int)response.StatusCode, basketId);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BasketResponse>(cancellationToken))?.Cart;
    }
}

public sealed class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger) : ICatalogClient
{
    public async Task<IReadOnlyDictionary<string, CatalogProduct>> GetProductsAsync(CancellationToken cancellationToken)
    {
        ProductPage page;
        try
        {
            page = await httpClient.GetFromJsonAsync<ProductPage>(
                "products?pageIndex=1&pageSize=500", cancellationToken)
                ?? throw new HttpRequestException("Catálogo devolvió una respuesta vacía.");
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Catalog service is unavailable");
            throw;
        }
        return page.Data.ToDictionary(product => product.Id, StringComparer.OrdinalIgnoreCase);
    }
}
