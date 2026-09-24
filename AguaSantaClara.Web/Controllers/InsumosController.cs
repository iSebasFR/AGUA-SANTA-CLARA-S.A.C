using AguaSantaClara.Web.Data;
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
}