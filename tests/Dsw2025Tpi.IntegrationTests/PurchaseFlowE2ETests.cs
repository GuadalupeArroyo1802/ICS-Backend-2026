using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class PurchaseFlowE2ETests
{
    private readonly CustomWebApplicationFactory _factory;

    public PurchaseFlowE2ETests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PurchaseFlow_RegisterLoginCreateOrderAndQuery_EndsWithOrderStoredAndStockUpdated()
    {
        // Paso 1: un admin se registra y obtiene su token
        var admin = await _factory.CreateAuthenticatedClientAsync("admin");

        // Paso 2: el admin crea un producto (stock 10, precio 100)
        var product = await ProductTestHelper.CreateAsync(admin, price: 100m, stock: 10);

        // Paso 3: un cliente se registra
        var client = _factory.CreateClient();
        var username = $"cliente_{Guid.NewGuid():N}"[..20];
        const string password = "Test1234!";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@test.com", password, role = "customer" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        // Paso 4: el cliente inicia sesión y usa el token del login
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await login.Content.ReadFromJsonAsync<AuthResult>();
        session!.CustomerId.Should().NotBeNull();
        client.WithBearer(session.Token);

        // Paso 5: el cliente crea una orden de 3 unidades
        var orderRequest = new OrderModel.OrderRequest(
            ShippingAddress: "Av. Siempre Viva 742",
            BillingAddress: "Av. Siempre Viva 742",
            CustomerId: session.CustomerId!.Value,
            OrderItems: new List<OrderModel.OrderItemModel> { new(product.Id, 3) });
        var created = await client.PostAsJsonAsync("/api/orders", orderRequest);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await created.Content.ReadFromJsonAsync<OrderModel.OrderResponse>();
        order!.TotalAmount.Should().Be(300m);

        // Paso 6: consulta la orden por id y en su listado
        var byId = await client.GetFromJsonAsync<OrderModel.OrderResponse>($"/api/orders/{order.Id}");
        byId!.OrderItems.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 3);

        var list = await client.GetFromJsonAsync<List<OrderModel.OrderResponse>>(
            $"/api/orders?customerId={session.CustomerId}");
        list.Should().Contain(o => o.Id == order.Id);

        // Paso 7: el stock del producto bajó a 7
        var after = await _factory.CreateClient()
            .GetFromJsonAsync<ProductModel.ProductResponseUpdate>($"/api/products/{product.Id}");
        after!.Stock.Should().Be(7);
    }
}