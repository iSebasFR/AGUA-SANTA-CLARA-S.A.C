using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora")]
public class InsumosController : Controller
{
    private readonly AppDbContext _context;

    public InsumosController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var consulta = _context.Insumos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();

            consulta = consulta.Where(i =>
                i.Nombre.Contains(termino));
        }

        var insumos = await consulta
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        ViewBag.Search = search;

        return View(insumos);
    }

    
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CrearInsumoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearInsumoViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var insumo = new Insumo
        {
            Nombre = model.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(model.Descripcion)
                ? null
                : model.Descripcion.Trim(),
            Costo = model.Costo,
            Estado = model.Estado,
            EstadoRegistro = true
        };

        _context.Insumos.Add(insumo);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Insumo creado correctamente";

        return RedirectToAction(nameof(Index));
    }

        [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insumo == null)
            return NotFound();

        var model = new EditarInsumoViewModel
        {
            Id = insumo.Id,
            Nombre = insumo.Nombre,
            Descripcion = insumo.Descripcion,
            Costo = insumo.Costo,
            Estado = insumo.Estado
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditarInsumoViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == model.Id);

        if (insumo == null)
            return NotFound();

        insumo.Nombre = model.Nombre.Trim();
        insumo.Descripcion = string.IsNullOrWhiteSpace(model.Descripcion)
            ? null
            : model.Descripcion.Trim();
        insumo.Costo = model.Costo;
        insumo.Estado = model.Estado;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Insumo actualizado correctamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(long id)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insumo == null)
            return NotFound();

        insumo.Estado = false;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Insumo desactivado correctamente";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insumo == null)
            return NotFound();

        _context.Insumos.Remove(insumo);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Insumo eliminado correctamente";

        return RedirectToAction(nameof(Index));
    }

}