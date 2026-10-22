using System.Security.Claims;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly UserManager<Usuario> _userManager;

    public AccountController(
        AppDbContext context,
        IPasswordHasher<Usuario> passwordHasher,
        UserManager<Usuario> userManager)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Tarea ASC-45: Buscar usuario en la BD por Username
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.UserName == model.Username && u.EstadoRegistro);

        if (usuario == null)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        // Bloqueo temporal tras varios intentos fallidos (protección contra fuerza bruta)
        if (await _userManager.IsLockedOutAsync(usuario))
        {
            ModelState.AddModelError(string.Empty,
                "Cuenta bloqueada temporalmente por intentos fallidos. Intente nuevamente en unos minutos.");
            return View(model);
        }

        // Tarea ASC-44: Validar la contraseña con el hasher oficial de Identity.
        var passwordValida = !string.IsNullOrWhiteSpace(usuario.PasswordHash)
            && _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, model.Password)
                != PasswordVerificationResult.Failed;

        if (!passwordValida)
        {
            await _userManager.AccessFailedAsync(usuario);
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        await _userManager.ResetAccessFailedCountAsync(usuario);

        // Regla de Negocio: Usuarios inactivos no pueden iniciar sesión.
        // Se valida después de la contraseña para no revelar qué usuarios existen.
        if (!usuario.Estado)
        {
            ModelState.AddModelError(string.Empty, "El usuario se encuentra inactivo. Contacte al administrador.");
            return View(model);
        }

        // Tarea ASC-46: Asignar Claims del Rol y Permisos
        var claims = new List<Claim>
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Name, usuario.UserName ?? model.Username),
            new System.Security.Claims.Claim("NombreCompleto", $"{usuario.Nombres} {usuario.Apellidos}"),
            new System.Security.Claims.Claim(ClaimTypes.Role, usuario.Rol?.Name ?? "SinRol")
        };

        var claimsIdentity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Tarea ASC-50: Redirigir según rol o ruta solicitada
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToAction("Login", "Account");
    }
}