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

    // ==================== T25: VALIDAR MODIFICACIÓN DE STOCK ====================
    [HttpPost("Modificar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Modificar(ModificarStockInsumoViewModel model)
    {
        var insumo = await _context.Insumos
            .FirstOrDefaultAsync(i => i.Id == model.Id && i.EstadoRegistro);

        if (insumo == null)
            return NotFound();

        // La cantidad se valida a mano para dar siempre el mismo mensaje
        // (cubre vacío, 0, negativos y decimales).
        ModelState.Remove(nameof(model.Cantidad));

        if (model.Cantidad == null || model.Cantidad <= 0)
        {
            ModelState.AddModelError(nameof(model.Cantidad),
                "La cantidad ingresada debe ser mayor a cero");
        }

        // Acción y motivo válidos
        if (string.IsNullOrWhiteSpace(model.Accion) ||
            !MotivosStockInsumo.PorAccion.TryGetValue(model.Accion, out var motivosPermitidos))
        {
            ModelState.AddModelError(nameof(model.Accion), "Seleccione una acción válida.");
        }
        else if (string.IsNullOrWhiteSpace(model.Motivo) ||
                 !motivosPermitidos.Contains(model.Motivo))
        {
            ModelState.AddModelError(nameof(model.Motivo), "Seleccione un motivo válido.");
        }

        // Reglas de stock (solo si la cantidad ya es válida)
        if (model.Cantidad.HasValue && model.Cantidad > 0)
        {
            if (model.Accion == "Descontar" && model.Cantidad > insumo.StockActual)
            {
                ModelState.AddModelError(nameof(model.Cantidad),
                    $"No se puede descontar más que el stock actual ({insumo.StockActual} unid.)");
            }
            else if (model.Accion == "Aumentar" &&
                     (long)insumo.StockActual + model.Cantidad.Value > int.MaxValue)
            {
                ModelState.AddModelError(nameof(model.Cantidad),
                    "La cantidad ingresada es demasiado grande");
            }
        }

        if (!ModelState.IsValid)
        {
            model.NombreInsumo = insumo.Nombre;
            model.StockActual = insumo.StockActual;
            return PartialView("_Modificar", model);
        }

                // ==================== T26: APLICAR EL CAMBIO DE STOCK ====================
        var cantidad = model.Cantidad!.Value;

        insumo.StockActual = model.Accion == "Aumentar"
            ? insumo.StockActual + cantidad
            : insumo.StockActual - cantidad;

        // AppDbContext solo sobrescribe SaveChanges() síncrono, así que se asigna a mano
        insumo.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new
        {
            ok = true,
            id = insumo.Id,
            stockActual = insumo.StockActual,
            clase = StockInsumoHelper.ClaseCss(insumo.StockActual, insumo.StockMinimo),
            mensaje = "Guardado Exitosamente"
        });
    }
}   