using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora")]
public class ProductosController : Controller
{
    private readonly AppDbContext _context;

    public ProductosController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var consulta = _context.Productos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();

            consulta = consulta.Where(p =>
                p.Nombre.Contains(termino));
        }

        var productos = await consulta
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        ViewBag.Search = search;

        return View(productos);
    }
}