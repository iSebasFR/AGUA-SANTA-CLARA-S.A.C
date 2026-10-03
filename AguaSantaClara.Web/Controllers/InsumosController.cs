using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora,Gerente,Vendedora")]
public class InsumosController : Controller
{
    private readonly AppDbContext _context;

    public InsumosController(AppDbContext context)
    {
        _context = context;
    }

    // ==================== INDEX SIN FILTROS ====================
    public async Task<IActionResult> Index()
    {
        var insumos = await _context.Insumos
            .Where(i => i.EstadoRegistro)
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        return View(insumos);
    }

    // ==================== CREATE ====================
    [HttpGet]
    [Authorize(Roles = "Administradora,Gerente")]
    public IActionResult Create()
    {
        return PartialView("_Create", new CrearInsumoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Create(CrearInsumoViewModel model)
    {
        // Validar nombre duplicado
        var nombreDuplicado = await _context.Insumos
            .AnyAsync(i => i.Nombre == model.Nombre.Trim() && i.EstadoRegistro);

        if (nombreDuplicado)
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un insumo con ese nombre.");

        if (!ModelState.IsValid)
            return PartialView("_Create", model);

        var insumo = new Insumo
        {
            Nombre = model.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim(),
            Costo = model.Costo,
            Estado = model.Estado,
            EstadoRegistro = true
        };

        _context.Insumos.Add(insumo);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "INSUMO CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== EDIT ====================
    [HttpGet]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(long id)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        var model = new EditarInsumoViewModel
        {
            Id = insumo.Id,
            Nombre = insumo.Nombre,
            Descripcion = insumo.Descripcion,
            Costo = insumo.Costo
        };

        return PartialView("_Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(EditarInsumoViewModel model)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == model.Id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        // Validar nombre duplicado (excepto el mismo insumo)
        var nombreDuplicado = await _context.Insumos
            .AnyAsync(i => i.Nombre == model.Nombre.Trim() && i.Id != model.Id && i.EstadoRegistro);

        if (nombreDuplicado)
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un insumo con ese nombre.");

        if (!ModelState.IsValid)
            return PartialView("_Edit", model);

        insumo.Nombre = model.Nombre.Trim();
        insumo.Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim();
        insumo.Costo = model.Costo;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "INSUMO ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== CAMBIAR ESTADO ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> CambiarEstado(long id, bool estado)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        insumo.Estado = estado;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = estado
            ? "INSUMO ACTIVADO CORRECTAMENTE"
            : "INSUMO DESACTIVADO CORRECTAMENTE";

        return RedirectToAction(nameof(Index));
    }

    // ==================== ELIMINAR ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Delete(long id)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        _context.Insumos.Remove(insumo);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "INSUMO ELIMINADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }
}