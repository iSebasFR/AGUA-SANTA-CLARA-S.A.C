using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora,Gerente,Vendedora")]
[Route("Inventario/Insumos")]
public class InventarioInsumosController : Controller
{
    private readonly AppDbContext _context;

    public InventarioInsumosController(AppDbContext context)
    {
        _context = context;
    }

    // ==================== LISTADO ====================
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var insumos = await _context.Insumos
            .AsNoTracking()
            .Where(i => i.EstadoRegistro)
            .OrderBy(i => i.LineaProducto)
            .ThenBy(i => i.Nombre)
            .ToListAsync();

        return View(insumos);
    }
}