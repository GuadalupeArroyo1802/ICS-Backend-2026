using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class ProductsIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange
        var anonymous = _factory.CreateClient();
        var customer = await _factory.CreateAuthenticatedClientAsync("customer");
        var request = ProductTestHelper.NewRequest();

        // Act
        var withoutToken = await anonymous.PostAsJsonAsync("/api/products", request);
        var asCustomer = await customer.PostAsJsonAsync("/api/products", request);

        // Assert
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asCustomer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
    {
        // Arrange
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var request = ProductTestHelper.NewRequest(price: 250m, stock: 7);

        // Act
        var response = await admin.PostAsJsonAsync("/api/products", request);

        // Assert: respuesta
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProductModel.ProductResponse>();
        body.Should().NotBeNull();
        body!.Sku.Should().Be(request.Sku);

        // Assert: el producto quedó guardado en la base SQLite
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        var saved = await db.Set<Product>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Sku == request.Sku);

        saved.Should().NotBeNull();
        saved!.Name.Should().Be(request.Name);
        saved.CurrentUnitPrice.Should().Be(250m);
        saved.StockQuantity.Should().Be(7);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsConflictStatusCode()
    {
        // Arrange
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var first = ProductTestHelper.NewRequest();
        var duplicated = ProductTestHelper.NewRequest(sku: first.Sku);

        // Act
        var firstResponse = await admin.PostAsJsonAsync("/api/products", first);
        var secondResponse = await admin.PostAsJsonAsync("/api/products", duplicated);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}