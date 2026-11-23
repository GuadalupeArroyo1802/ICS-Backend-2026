using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;   // si AuthenticateContext o Dsw2025TpiContext quedan en rojo, ajustá este namespace
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        // Configuración JWT de pruebas: Program.cs la lee al arrancar, por eso va como variable de entorno
        Environment.SetEnvironmentVariable("Jwt__Key", "clave-solo-para-pruebas-de-32-caracteres-minimo");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Dsw2025Tpi.Api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Dsw2025Tpi.ApiUsers");
        Environment.SetEnvironmentVariable("Jwt__ExpireInMinutes", "60");

        // SQLite en memoria compartida: vive mientras esta conexión esté abierta
        _connection = new SqliteConnection("DataSource=file:memdb?mode=memory&cache=shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            UsarSqlite<AuthenticateContext>(services);
            UsarSqlite<Dsw2025TpiContext>(services);

            // Crear las tablas de ambos contextos en la misma base SQLite
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AuthenticateContext>();
            var negocio = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
            auth.Database.EnsureCreated();
            negocio.Database.GetService<IRelationalDatabaseCreator>().CreateTables();
        });
    }

    private void UsarSqlite<T>(IServiceCollection services) where T : DbContext
    {
        var aQuitar = services.Where(d =>
            d.ServiceType == typeof(DbContextOptions<T>) ||
            (d.ServiceType.IsGenericType &&
             d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration") &&
             d.ServiceType.GenericTypeArguments[0] == typeof(T)))
            .ToList();

        foreach (var d in aQuitar) services.Remove(d);

        services.AddDbContext<T>(o => o.UseSqlite(_connection));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}