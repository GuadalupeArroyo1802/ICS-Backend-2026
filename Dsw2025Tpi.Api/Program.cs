using Dsw2025Tpi.Data;
using Dsw2025Tpi.Data.Repositories;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Dsw2025Tpi.Data.helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Dsw2025Tpi.Application.Services;



namespace Dsw2025Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args); 


        builder.Services.AddControllers() //dan soporte a [ApíControllers]
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });
        // https://aka.ms/aspnetcore/swashbuckle 
      
        builder.Services.AddScoped<IProductsManagementService, ProductsManagementService>(); //logica del negocio del negicio
        builder.Services.AddScoped<IOrderManagement, OrderManagement>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddTransient<IRepository, EfRepository>();
        builder.Services.AddSwaggerGen(o => //configuracion de swagger
        {
            o.SwaggerDoc("v1", new OpenApiInfo 
            {
                Title = "Desarrollo De Software",
                Version = "v1", 
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme //definicion de esquema de seguridad
            {
                In = ParameterLocation.Header,
                Name = "Authorization", 
                Description = "Ingresar el token", 
                Type = SecuritySchemeType.ApiKey,
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement //indica que todos los endpoints requieren autenticacion
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                            
                        }
                    },
                    new string[] {}
                }
            });
        });
        builder.Services.AddDbContext<Dsw2025TpiContext>(options =>
        {

            options.UseSqlServer(builder.Configuration.GetConnectionString("Dsw2025TpiEntities"));
        });
        builder.Services.AddHealthChecks();

        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => 
        {
            options.Password = new PasswordOptions 
            {
                RequiredLength = 8
            };
        })
        .AddEntityFrameworkStores<AuthenticateContext>() 
        .AddDefaultTokenProviders(); 


        var jwtConfig = builder.Configuration.GetSection("Jwt"); 
        var keyText = jwtConfig["Key"] ?? throw new ArgumentNullException("Jwt Key"); 
        var key = Encoding.UTF8.GetBytes(keyText); 

        builder.Services.AddAuthentication(options => 
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
           .AddJwtBearer(options =>
           {
               options.TokenValidationParameters = new TokenValidationParameters 
               {
                   ValidateIssuer = true,
                   ValidateAudience = true, 
                   ValidateLifetime = true, 
                   ValidateIssuerSigningKey = true, 
                   ValidIssuer = jwtConfig["Issuer"], 
                   ValidAudience = jwtConfig["Audience"], 
                   IssuerSigningKey = new SymmetricSecurityKey(key) 
               };
           });

        builder.Services.AddSingleton<JwtTokenService>(); 

        builder.Services.AddDbContext<AuthenticateContext>(options => 
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("Dsw2025TpiEntities")); 

        });


        builder.Services.AddCors(options => 
        {
            options.AddPolicy("PermitirFrontend", policy =>
                policy.WithOrigins("http://localhost:3000") 
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        var app = builder.Build(); 
        using (var scope = app.Services.CreateScope()) 
        {
            var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>(); 
            db.Seedwork<Customer>(Path.Combine(AppContext.BaseDirectory, "Sources", "customers.json"));
            db.Seedwork<Product>(Path.Combine(AppContext.BaseDirectory, "Sources", "products.json"));
            db.Seedwork<Order>(Path.Combine(AppContext.BaseDirectory, "Sources", "orders.json"));


            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(); 
            string[] roles = new[] { "admin", "customer" }; 

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role)) 
                {
                    await roleManager.CreateAsync(new IdentityRole(role)); 
                }
            }
        }
        
        app.UseSwagger(); 
        app.UseSwaggerUI();

        // pruebwa de cors

        app.UseHttpsRedirection(); 

        app.UseAuthentication(); 

        app.UseAuthorization(); 

        app.MapControllers(); 

        app.MapHealthChecks("/healthcheck"); 

        app.Run(); 
    }
}