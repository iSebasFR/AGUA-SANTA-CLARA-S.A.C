using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    // ==================== INDEX CON FILTROS ====================
    public async Task<IActionResult> Index(string? search, long? idRol, bool? estado)
    {
        var consulta = _context.Usuarios
            .Include(u => u.Rol)
            .Where(u => u.Rol!.Codigo != "GERENTE") // Ocultar al Gerente
            .AsQueryable();

        // Filtro por búsqueda (usuario o nombre completo)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();
            var patron = $"%{termino}%";
            consulta = consulta.Where(u =>
                EF.Functions.ILike(u.Nombres + " " + u.Apellidos, patron) ||
                EF.Functions.ILike(u.Apellidos + " " + u.Nombres, patron) ||
                EF.Functions.ILike(u.UserName ?? string.Empty, patron));
        }

        // Filtro por rol
        if (idRol.HasValue)
        {
            consulta = consulta.Where(u => u.IdRol == idRol.Value);
        }

        // Filtro por estado
        if (estado.HasValue)
        {
            consulta = consulta.Where(u => u.Estado == estado.Value);
        }

        var usuarios = await consulta
            .OrderBy(u => u.Apellidos)
            .ThenBy(u => u.Nombres)
            .ToListAsync();

        var model = new UsuariosIndexViewModel
        {
            Usuarios = usuarios,
            Search = search,
            IdRolFiltro = idRol,
            EstadoFiltro = estado,
            Roles = await _context.Roles
                .Where(r => r.Estado && r.EstadoRegistro && r.Codigo != "GERENTE")
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.Codigo == "ADMIN" ? "Administrador" : "Vendedor"
                })
                .ToListAsync()
        };

        return View(model);
    }

    // ==================== CREATE ====================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CrearUsuarioViewModel
        {
            Roles = await ObtenerRolesAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearUsuarioViewModel model)
    {
        // Validar que el UserName no exista
        var userNameDuplicado = await _context.Usuarios
            .AnyAsync(u => u.UserName == model.UserName.Trim());

        if (userNameDuplicado)
            ModelState.AddModelError(nameof(model.UserName), "Ya existe un usuario con ese nombre.");

        var rol = model.IdRol.HasValue
            ? await _context.Roles.FirstOrDefaultAsync(r =>
                r.Id == model.IdRol.Value && r.Estado && r.EstadoRegistro && r.Codigo != "GERENTE")
            : null;

        if (rol == null)
            ModelState.AddModelError(nameof(model.IdRol), "Seleccione un rol válido.");

        if (!ModelState.IsValid)
        {
            model.Roles = await ObtenerRolesAsync();
            return View(model);
        }

        var usuario = new Usuario
        {
            UserName = model.UserName.Trim(),
            NormalizedUserName = model.UserName.Trim().ToUpper(),
            Email = $"{model.UserName.Trim()}@aguasantaclara.local",
            NormalizedEmail = $"{model.UserName.Trim()}@aguasantaclara.local".ToUpper(),
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

            model.Roles = await ObtenerRolesAsync();
            return View(model);
        }

        var asignacion = await _userManager.AddToRoleAsync(usuario, rol.Name!);
        if (!asignacion.Succeeded)
        {
            foreach (var error in asignacion.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await _userManager.DeleteAsync(usuario);
            model.Roles = await ObtenerRolesAsync();
            return View(model);
        }

        TempData["SuccessMessage"] = "USUARIO CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== EDIT ====================
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
                .ThenInclude(r => r.RolPermisos)
                .ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null || usuario.Rol?.Codigo == "GERENTE")
            return NotFound();

        var model = new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            UserName = usuario.UserName ?? string.Empty,
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
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
                .ToList() ?? [],
            Roles = await ObtenerRolesAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditarUsuarioViewModel model)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == model.Id);

        if (usuario == null || usuario.Rol?.Codigo == "GERENTE")
            return NotFound();

        // Validar que el nuevo UserName no exista en otro usuario
        var userNameDuplicado = await _context.Usuarios
            .AnyAsync(u => u.UserName == model.UserName.Trim() && u.Id != model.Id);

        if (userNameDuplicado)
            ModelState.AddModelError(nameof(model.UserName), "Ya existe un usuario con ese nombre.");

        var rol = model.IdRol.HasValue
            ? await _context.Roles.FirstOrDefaultAsync(r =>
                r.Id == model.IdRol.Value && r.Estado && r.EstadoRegistro && r.Codigo != "GERENTE")
            : null;

        if (rol == null)
            ModelState.AddModelError(nameof(model.IdRol), "Seleccione un rol válido.");

        if (!ModelState.IsValid)
        {
            model.Roles = await ObtenerRolesAsync();
            model.NombreRol = usuario.Rol?.Codigo == "ADMIN"
                ? "Administrador"
                : usuario.Rol?.Codigo == "VENDEDORA"
                    ? "Vendedor"
                    : usuario.Rol?.Name ?? "Sin rol";
            return View(model);
        }

        // Actualizar campos
        usuario.Nombres = model.Nombres.Trim();
        usuario.Apellidos = model.Apellidos.Trim();
        usuario.UserName = model.UserName.Trim();
        usuario.NormalizedUserName = model.UserName.Trim().ToUpper();
        usuario.IdRol = rol!.Id;

        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            model.Roles = await ObtenerRolesAsync();
            return View(model);
        }

        // Actualizar rol
        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        if (rolesActuales.Count > 0)
            await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
        await _userManager.AddToRoleAsync(usuario, rol.Name!);

        TempData["SuccessMessage"] = "USUARIO ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== CAMBIAR ESTADO (desde dropdown en la lista) ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id, bool estado)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null || usuario.Rol?.Codigo == "GERENTE")
            return NotFound();

        usuario.Estado = estado;
        var resultado = await _userManager.UpdateAsync(usuario);

        if (!resultado.Succeeded)
            TempData["ErrorMessage"] = string.Join(" | ", resultado.Errors.Select(e => e.Description));
        else
            TempData["SuccessMessage"] = estado
                ? "USUARIO ACTIVADO CORRECTAMENTE"
                : "USUARIO DESACTIVADO CORRECTAMENTE";

        return RedirectToAction(nameof(Index));
    }

    // ==================== RESTABLECER CONTRASEÑA ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestablecerPassword(RestablecerPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Verifique los datos del formulario.";
            return RedirectToAction(nameof(Index));
        }

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == model.Id);

        if (usuario == null || usuario.Rol?.Codigo == "GERENTE")
            return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, model.NuevaPassword);

        if (!resultado.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" | ", resultado.Errors.Select(e => e.Description));
        }
        else
        {
            TempData["SuccessMessage"] = "CONTRASEÑA RESTABLECIDA CORRECTAMENTE";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==================== ELIMINAR ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(long id)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null || usuario.Rol?.Codigo == "GERENTE")
            return NotFound();

        var resultado = await _userManager.DeleteAsync(usuario);

        if (!resultado.Succeeded)
            TempData["ErrorMessage"] = string.Join(" | ", resultado.Errors.Select(e => e.Description));
        else
            TempData["SuccessMessage"] = "USUARIO ELIMINADO CORRECTAMENTE";

        return RedirectToAction(nameof(Index));
    }

    // ==================== HELPERS ====================
    private async Task<IEnumerable<SelectListItem>> ObtenerRolesAsync()
    {
        return await _context.Roles
            .Where(r => r.Estado && r.EstadoRegistro && r.Codigo != "GERENTE")
            .OrderBy(r => r.Name)
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Codigo == "ADMIN" ? "Administrador" : "Vendedor"
            })
            .ToListAsync();
    }
}