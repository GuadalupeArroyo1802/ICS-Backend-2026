using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Dsw2025Tpi.IntegrationTests;

public record AuthResult(string Message, string Token, string Role, Guid? CustomerId);

public static class AuthHelper
{
    public static async Task<AuthResult> RegisterAsync(HttpClient client, string role)
    {
        var username = $"{role}_{Guid.NewGuid():N}"[..20];
        var body = new
        {
            username,
            email = $"{username}@test.com",
            password = "Test1234!",
            role
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        return auth!;
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Crea un cliente HTTP ya autenticado con un usuario nuevo del rol indicado.
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        this CustomWebApplicationFactory factory, string role)
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client, role);
        return client.WithBearer(auth.Token);
    }

    // Crea un cliente autenticado como "customer" y devuelve también su id.
    public static async Task<(HttpClient Client, Guid CustomerId)> CreateCustomerClientAsync(
        this CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var auth = await RegisterAsync(client, "customer");
        client.WithBearer(auth.Token);
        return (client, auth.CustomerId!.Value);
    }
}