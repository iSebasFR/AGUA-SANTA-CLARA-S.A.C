using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora")]
[Route("Inventario/Productos")]
public class InventarioProductosController : Controller
{
    private readonly AppDbContext _context;

    public InventarioProductosController(AppDbContext context)
    {
        _context = context;
    }

    // ==================== T18: LISTADO Y SELECTOR DE LOCALES ====================
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var locales = await _context.Locales
            .AsNoTracking()
            .Where(l => l.EstadoRegistro && l.Estado)
            .OrderBy(l => l.Nombre)
            .ToListAsync();

        return View(locales);
    }
    
    // ==================== T19: PRODUCTOS POR LOCAL ====================
[HttpGet("ProductosPorLocal")]
public async Task<IActionResult> ProductosPorLocal(long idLocal)
{
    var productos = await _context.ProductosLocal
        .AsNoTracking()
        .Where(pl =>
            pl.IdLocal == idLocal &&
            pl.Estado &&
            pl.EstadoRegistro &&
            pl.Producto.Estado &&
            pl.Producto.EstadoRegistro)
        .OrderBy(pl => pl.Producto.Categoria)
        .ThenBy(pl => pl.Producto.Nombre)
        .Select(pl => new
        {
            id = pl.IdProducto,
            nombre = pl.Producto.Nombre,
            categoria = pl.Producto.Categoria,
            stock = pl.Stock
        })
        .ToListAsync();

    return Json(productos);
}
}