using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

public class UsuariosController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public UsuariosController(AppDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var usuarios = await _context.Usuarios
            .Include(u => u.Rol)
            .OrderBy(u => u.Apellidos)
            .ThenBy(u => u.Nombres)
            .ToListAsync();

        return View(usuarios);
    }

    [HttpGet]
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Create()
    {
        var model = new CrearUsuarioViewModel
        {
            Roles = await _context.Roles
                .Where(r => r.Estado && r.EstadoRegistro)
                .Where(r => r.Codigo != "GERENTE")
                .OrderBy(r => r.Name)
                .Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.Codigo == "ADMIN" ? "Administrador" : "Vendedor"
                })
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Create(CrearUsuarioViewModel model)
    {
        var rol = model.IdRol.HasValue
            ? await _context.Roles.FirstOrDefaultAsync(r =>
                r.Id == model.IdRol.Value &&
                r.Estado &&
                r.EstadoRegistro &&
                r.Codigo != "GERENTE")
            : null;

        if (rol == null)
            ModelState.AddModelError(nameof(model.IdRol), "Seleccione un rol válido.");

        if (!ModelState.IsValid)
        {
            await CargarRolesAsync(model);
            return View(model);
        }

        var usuario = new Usuario
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            Nombres = model.Nombres.Trim(),
            Apellidos = model.Apellidos.Trim(),
            IdRol = rol!.Id,
            Estado = true,
            EstadoRegistro = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("D")
        };

        var resultado = await _userManager.CreateAsync(usuario, model.Password);

        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await CargarRolesAsync(model);
            return View(model);
        }

        var asignacion = await _userManager.AddToRoleAsync(usuario, rol.Name!);
        if (!asignacion.Succeeded)
        {
            foreach (var error in asignacion.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await _userManager.DeleteAsync(usuario);
            await CargarRolesAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = "USUARIO CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarRolesAsync(CrearUsuarioViewModel model)
    {
        model.Roles = await _context.Roles
            .Where(r => r.Estado && r.EstadoRegistro && r.Codigo != "GERENTE")
            .OrderBy(r => r.Name)
            .Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Codigo == "ADMIN" ? "Administrador" : "Vendedor"
            })
            .ToListAsync();
    }
}