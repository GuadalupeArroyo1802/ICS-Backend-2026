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
public class OrdersIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static OrderModel.OrderRequest NewOrder(Guid customerId, Guid productId, int quantity)
        => new(
            ShippingAddress: "Av. Siempre Viva 742",
            BillingAddress: "Av. Siempre Viva 742",
            CustomerId: customerId,
            OrderItems: new List<OrderModel.OrderItemModel> { new(productId, quantity) });

    [Fact]
    public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var anonymous = _factory.CreateClient();
        var request = NewOrder(Guid.NewGuid(), Guid.NewGuid(), 1);

        // Act
        var response = await anonymous.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateOrder_WhenAuthenticatedWithValidData_ReturnsCreatedAndPersistsOrderAndStockInSqlite()
    {
        // Arrange: producto con stock 10 y precio 100; un customer
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var product = await ProductTestHelper.CreateAsync(admin, price: 100m, stock: 10);
        var (customer, customerId) = await _factory.CreateCustomerClientAsync();
        var request = NewOrder(customerId, product.Id, quantity: 3);

        // Act
        var response = await customer.PostAsJsonAsync("/api/orders", request);

        // Assert: respuesta
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderModel.OrderResponse>();
        order.Should().NotBeNull();
        order!.CustomerId.Should().Be(customerId);
        order.Status.Should().Be("Pending");
        order.TotalAmount.Should().Be(300m);
        order.OrderItems.Should().ContainSingle(i =>
            i.ProductId == product.Id && i.Quantity == 3 && i.Subtotal == 300m);

        // Assert: la orden y el nuevo stock quedaron guardados en SQLite
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var savedOrder = await db.Set<Order>().AsNoTracking()
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == order.Id);
        savedOrder.Should().NotBeNull();
        savedOrder!.CustomerId.Should().Be(customerId);
        savedOrder.OrderItems.Should().HaveCount(1);

        var savedProduct = await db.Set<Product>().AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);
        savedProduct.StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestWithErrorMessage()
    {
        // Arrange: producto con stock 2; se piden 5
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");
        var product = await ProductTestHelper.CreateAsync(admin, price: 100m, stock: 2);
        var (customer, customerId) = await _factory.CreateCustomerClientAsync();
        var request = NewOrder(customerId, product.Id, quantity: 5);

        // Act
        var response = await customer.PostAsJsonAsync("/api/orders", request);

        // Assert: 400 con mensaje de stock
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("No hay suficiente stock").And.Contain(product.Name);

        // Assert: el stock no cambió
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        var savedProduct = await db.Set<Product>().AsNoTracking()
            .FirstAsync(p => p.Id == product.Id);
        savedProduct.StockQuantity.Should().Be(2);
    }
}