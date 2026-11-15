using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class ProductsQueryIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsQueryIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Crea un producto por la API (como admin) y devuelve la respuesta.
    private static async Task<ProductModel.ProductResponse> CreateProductAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/products", ProductTestHelper.NewRequest(name: name));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>())!;
    }

    [Fact]
    public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
    {
        // Arrange: un producto activo para que la lista no esté vacía
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var created = await CreateProductAsync(admin, $"Publico-{Guid.NewGuid():N}"[..14]);
        var anonymous = _factory.CreateClient();

        // Act: el endpoint público no requiere token
        var response = await anonymous.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var products = await response.Content
            .ReadFromJsonAsync<List<ProductModel.ProductResponseUpdate>>();
        products.Should().NotBeNullOrEmpty();
        products.Should().Contain(p => p.Sku == created.Sku);
        products.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite()
    {
        // Arrange: 3 productos con una etiqueta única en el nombre
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (var i = 1; i <= 3; i++)
            await CreateProductAsync(admin, $"Filtro-{tag}-{i}");

        // Act: buscar por la etiqueta, de a 2 por página
        var page1 = await admin.GetFromJsonAsync<ProductModel.ResponsePagination>(
            $"/api/products/admin?search={tag}&pageNumber=1&pageSize=2");
        var page2 = await admin.GetFromJsonAsync<ProductModel.ResponsePagination>(
            $"/api/products/admin?search={tag}&pageNumber=2&pageSize=2");

        // Assert
        page1.Should().NotBeNull();
        page1!.Total.Should().Be(3);
        page1.ProductItems.Should().HaveCount(2);
        page1.ProductItems.Should().OnlyContain(p => p.Name.Contains(tag));

        page2.Should().NotBeNull();
        page2!.Total.Should().Be(3);
        page2.ProductItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
    {
        // Arrange: dos productos; uno se deshabilita
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var active = await CreateProductAsync(admin, $"Activo-{tag}");
        var toDisable = await CreateProductAsync(admin, $"Inactivo-{tag}");

        var disable = await admin.PatchAsync(
            $"/api/products/{toDisable.Id}", new StringContent(string.Empty));
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var all = await admin.GetFromJsonAsync<ProductModel.ResponsePagination>(
            $"/api/products/admin?search={tag}");
        var onlyDisabled = await admin.GetFromJsonAsync<ProductModel.ResponsePagination>(
            $"/api/products/admin?search={tag}&status=disabled");
        var publicList = await _factory.CreateClient()
            .GetFromJsonAsync<List<ProductModel.ProductResponseUpdate>>("/api/products");

        // Assert: admin ve ambos; el filtro "disabled" ve solo el inactivo
        all!.Total.Should().Be(2);
        all.ProductItems.Should().Contain(p => p.Sku == toDisable.Sku && !p.isActive);
        all.ProductItems.Should().Contain(p => p.Sku == active.Sku && p.isActive);

        onlyDisabled!.Total.Should().Be(1);
        onlyDisabled.ProductItems.Single().Sku.Should().Be(toDisable.Sku);

        // El listado público no incluye el producto inactivo
        publicList.Should().NotContain(p => p.Sku == toDisable.Sku);
    }
}