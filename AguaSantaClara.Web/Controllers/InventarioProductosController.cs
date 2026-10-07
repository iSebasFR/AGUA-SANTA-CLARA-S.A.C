using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
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
// ==================== T20: MOSTRAR VENTANA DE MODIFICACIÓN ====================
[HttpGet("Modificar/{idLocal:long}/{idProducto:long}")]
public async Task<IActionResult> Modificar(long idLocal, long idProducto)
{
    var productoLocal = await _context.ProductosLocal
        .AsNoTracking()
        .Include(pl => pl.Producto)
        .FirstOrDefaultAsync(pl =>
            pl.IdLocal == idLocal &&
            pl.IdProducto == idProducto &&
            pl.Estado &&
            pl.EstadoRegistro &&
            pl.Producto.Estado &&
            pl.Producto.EstadoRegistro);

    if (productoLocal == null)
        return NotFound();

    var model = new AguaSantaClara.Web.Models.ModificarStockProductoViewModel
    {
        IdProducto = productoLocal.IdProducto,
        IdLocal = productoLocal.IdLocal,
        NombreProducto = productoLocal.Producto.Nombre,
        StockActual = productoLocal.Stock,
        Accion = "Aumentar",
        Motivo = StockProductoHelper.PorAccion["Aumentar"][0]
    };

    return PartialView("_Modificar", model);
}
// ==================== T21: VALIDAR MODIFICACIÓN DE STOCK ====================
[HttpPost("Modificar")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Modificar(ModificarStockProductoViewModel model)
{
    var productoLocal = await _context.ProductosLocal
        .FirstOrDefaultAsync(pl =>
            pl.IdLocal == model.IdLocal &&
            pl.IdProducto == model.IdProducto &&
            pl.Estado &&
            pl.EstadoRegistro);

    if (productoLocal == null)
        return NotFound();

    ModelState.Remove(nameof(model.Cantidad));

    // Validar cantidad
    if (model.Cantidad == null || model.Cantidad <= 0)
    {
        ModelState.AddModelError(
            nameof(model.Cantidad),
            "La cantidad debe ser un número entero mayor a cero.");
    }

    // Validar acción
    if (string.IsNullOrWhiteSpace(model.Accion) ||
        !StockProductoHelper.PorAccion.ContainsKey(model.Accion))
    {
        ModelState.AddModelError(
            nameof(model.Accion),
            "Seleccione una acción válida.");
    }

    // Validar motivo
    if (string.IsNullOrWhiteSpace(model.Motivo) ||
        !StockProductoHelper.PorAccion.TryGetValue(
            model.Accion ?? "",
            out var motivosPermitidos) ||
        !motivosPermitidos.Contains(model.Motivo))
    {
        ModelState.AddModelError(
            nameof(model.Motivo),
            "Seleccione un motivo válido.");
    }

    // Validar que el descuento no deje stock negativo
    if (model.Cantidad.HasValue &&
        model.Cantidad > 0 &&
        model.Accion == "Descontar" &&
        model.Cantidad > productoLocal.Stock)
    {
        ModelState.AddModelError(
            nameof(model.Cantidad),
            $"No se puede descontar más del stock disponible ({productoLocal.Stock} unid.).");
    }

    if (!ModelState.IsValid)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.Id == model.IdProducto &&
                p.Estado &&
                p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        model.NombreProducto = producto.Nombre;
        model.StockActual = productoLocal.Stock;

        return PartialView("_Modificar", model);
    }

    // ==================== T22: ACTUALIZAR STOCK ====================

if (model.Accion == "Aumentar")
{
    productoLocal.Stock += model.Cantidad!.Value;
}
else if (model.Accion == "Descontar")
{
    productoLocal.Stock -= model.Cantidad!.Value;
}

await _context.SaveChangesAsync();

return Json(new
{
    ok = true,
    mensaje = "Stock actualizado correctamente.",
    nuevoStock = productoLocal.Stock
});
}
}