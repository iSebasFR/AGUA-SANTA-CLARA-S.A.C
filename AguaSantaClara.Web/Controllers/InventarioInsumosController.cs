using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
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

    // ==================== VENTANA MODIFICAR CANTIDAD ====================
    [HttpGet("Modificar/{id:long}")]
    public async Task<IActionResult> Modificar(long id)
    {
        var insumo = await _context.Insumos
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        var model = new ModificarStockInsumoViewModel
        {
            Id = insumo.Id,
            NombreInsumo = insumo.Nombre,
            StockActual = insumo.StockActual,
            Accion = "Aumentar",
            Motivo = MotivosStockInsumo.PorAccion["Aumentar"][0]
        };

        return PartialView("_Modificar", model);
    }
}