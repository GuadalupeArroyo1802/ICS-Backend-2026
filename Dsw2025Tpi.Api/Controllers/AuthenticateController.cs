using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;                 
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Dsw2025Tpi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticateController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly Dsw2025TpiContext _dbContext;    

    public AuthenticateController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        JwtTokenService jwtTokenService,
        Dsw2025TpiContext dbContext     
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel request)
    {
        var user = await _userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            return Unauthorized("Usuario incorrecto o inexistente");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
        {
            return Unauthorized("Contraseña incorrecta");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles == null || roles.Count == 0)
        {
            return Unauthorized("El usuario no tiene un rol asignado.");
        }

        var role = roles.First();
        var token = _jwtTokenService.GenerateToken(request.Username, role);

        Customer? customer = null;

        if (role == "customer")
        {
            customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.Email == user.Email);
        }

        return Ok(new
        {
            message = "Usuario registrado correctamente.",
            token,
            role,
            customerId = customer?.Id
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        try
        {
            var emailCheck = new System.Net.Mail.MailAddress(model.Email);
        }
        catch (FormatException)
        {
            return BadRequest("El correo electrónico tiene un formato inválido.");
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return BadRequest("El correo electrónico ya está registrado.");
        }

        var role = string.IsNullOrWhiteSpace(model.Role) ? "customer" : model.Role.ToLower();
        if (role != "admin" && role != "customer")
        {
            return BadRequest("Rol inválido. Solo se permiten 'admin' o 'customer'.");
        }

        var user = new IdentityUser { UserName = model.Username, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, role);

        Customer? customer = null;

        if (role == "customer")
        {
            customer = new Customer(
                email: model.Email,
                name: model.Username,
                phoneNumber: ""
            );

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();
        }

        var token = _jwtTokenService.GenerateToken(user.UserName!, role);

        return Ok(new
        {
            message = "Usuario registrado correctamente.",
            token,
            role,
            customerId = customer?.Id
        }); 

    }

}
