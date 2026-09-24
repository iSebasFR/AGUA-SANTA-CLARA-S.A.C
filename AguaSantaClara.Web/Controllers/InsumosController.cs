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
}