using Dsw2025Tpi.Application.Dtos;
using System.Net.Http.Json;

namespace Dsw2025Tpi.IntegrationTests;

public static class ProductTestHelper
{
    // Arma un pedido de producto válido con SKU y código únicos.
    public static ProductModel.ProductRequest NewRequest(
        string? sku = null, string? name = null, decimal price = 100m, int stock = 10)
    {
        var code = Guid.NewGuid().ToString("N");
        return new ProductModel.ProductRequest(
            Sku: sku ?? $"IT-{code[..8]}",
            InternalCode: $"INT-{code[8..16]}",
            Name: name ?? $"Producto {code[..6]}",
            Description: "Producto creado por una prueba de integración",
            CurrentUnitPrice: price,
            StockQuantity: stock);
    }

    // Crea un producto por la API (como admin) y devuelve la respuesta.
    public static async Task<ProductModel.ProductResponse> CreateAsync(
        HttpClient admin, decimal price = 100m, int stock = 10)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/products", NewRequest(price: price, stock: stock));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!;
    }
}