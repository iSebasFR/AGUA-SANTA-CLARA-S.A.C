using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Gerente")]
public class UsuariosController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public UsuariosController(AppDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var consulta = _context.Usuarios
            .Include(u => u.Rol)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();
            var patron = $"%{termino}%";
            consulta = consulta.Where(u =>
                EF.Functions.ILike(u.Nombres + " " + u.Apellidos, patron) ||
                EF.Functions.ILike(u.Apellidos + " " + u.Nombres, patron) ||
                EF.Functions.ILike(u.UserName ?? string.Empty, patron) ||
                EF.Functions.ILike(u.Email ?? string.Empty, patron) ||
                EF.Functions.ILike(u.Rol!.Codigo, patron) ||
                EF.Functions.ILike(u.Rol!.Name ?? string.Empty, patron));
        }

        var usuarios = await consulta
            .OrderBy(u => u.Apellidos)
            .ThenBy(u => u.Nombres)
            .ToListAsync();

        ViewBag.Search = search;
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

    [HttpGet]
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Edit(long id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
                .ThenInclude(r => r.RolPermisos)
                .ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null)
            return NotFound();

        var model = new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            UserName = usuario.UserName ?? string.Empty,
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
            Email = usuario.Email ?? string.Empty,
            IdRol = usuario.IdRol,
            Estado = usuario.Estado,
            NombreRol = usuario.Rol?.Codigo == "ADMIN"
                ? "Administrador"
                : usuario.Rol?.Codigo == "VENDEDORA"
                    ? "Vendedor"
                    : usuario.Rol?.Name ?? "Sin rol",
            Permisos = usuario.Rol?.RolPermisos
                .Where(rp => rp.EstadoRegistro && rp.Permiso.Estado && rp.Permiso.EstadoRegistro)
                .OrderBy(rp => rp.Permiso.Nombre)
                .Select(rp => rp.Permiso.Nombre)
                .ToList() ?? []
        };

        await CargarRolesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Edit(EditarUsuarioViewModel model)
    {
        var usuario = await _context.Usuarios.FindAsync(model.Id);
        var rol = model.IdRol.HasValue
            ? await _context.Roles.FirstOrDefaultAsync(r =>
                r.Id == model.IdRol.Value &&
                r.Estado &&
                r.EstadoRegistro &&
                r.Codigo != "GERENTE")
            : null;

        if (usuario == null)
            return NotFound();

        if (rol == null)
            ModelState.AddModelError(nameof(model.IdRol), "Seleccione un rol válido.");

        if (!ModelState.IsValid)
        {
            await CargarRolesAsync(model);
            return View(model);
        }

        usuario.Nombres = model.Nombres.Trim();
        usuario.Apellidos = model.Apellidos.Trim();
        usuario.Email = model.Email.Trim();
        usuario.IdRol = rol!.Id;

        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await CargarRolesAsync(model);
            return View(model);
        }

        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        if (rolesActuales.Count > 0)
            await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
        await _userManager.AddToRoleAsync(usuario, rol.Name!);

        TempData["SuccessMessage"] = "USUARIO ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Gerente")]
    public async Task<IActionResult> Deactivate(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return NotFound();

        usuario.Estado = false;
        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                TempData["ErrorMessage"] = error.Description;
        }
        else
        {
            TempData["SuccessMessage"] = "USUARIO DESACTIVADO CORRECTAMENTE";
        }

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

    private async Task CargarRolesAsync(EditarUsuarioViewModel model)
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