using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
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
        var localValido = await _context.Locales
            .AsNoTracking()
            .AnyAsync(l =>
                l.Id == idLocal &&
                l.Estado &&
                l.EstadoRegistro);

        if (!localValido)
            return BadRequest(new { mensaje = "Seleccione un local válido." });

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
                id = pl.Producto.Id,
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

        var model = new ModificarStockProductoViewModel
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

        if (model.Cantidad == null || model.Cantidad <= 0)
        {
            ModelState.AddModelError(
                nameof(model.Cantidad),
                "La cantidad debe ser un número entero mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(model.Accion) ||
            !StockProductoHelper.PorAccion.ContainsKey(model.Accion))
        {
            ModelState.AddModelError(
                nameof(model.Accion),
                "Seleccione una acción válida.");
        }

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

        var cambios = await _context.SaveChangesAsync();

        return Json(new
        {
            ok = true,
            mensaje = "GUARDADO EXITOSAMENTE",
            stockFinal = productoLocal.Stock,
            cambiosGuardados = cambios
        });
    }

    // ==================== T23: MOSTRAR VENTANA BALANCEAR BIDONES ====================
    [HttpGet("Balancear/{idLocal:long}/{idProducto:long}")]
    public async Task<IActionResult> Balancear(long idLocal, long idProducto)
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

        if (!string.Equals(productoLocal.Producto.Categoria, "Bidón", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Solo se pueden balancear productos de categoría Bidón." });

        var model = new BalancearBidonesViewModel
        {
            IdProducto = productoLocal.IdProducto,
            IdLocal = productoLocal.IdLocal,
            NombreProducto = productoLocal.Producto.Nombre,
            StockActual = productoLocal.Stock,
            TipoMovimiento = "Entrega",
            AccionProduccion = "Aumentar",
            Motivo = "Entrega a cliente"
        };

        return PartialView("_Balancear", model);
    }

    // ==================== T24: BUSCAR CLIENTES (auto-completado) ====================
    [HttpGet("BuscarClientes")]
    public async Task<IActionResult> BuscarClientes(string q, int limite = 8)
    {
        var valor = q?.Trim();
        if (string.IsNullOrWhiteSpace(valor) || valor.Length < 2)
            return Json(Array.Empty<object>());

        var clientes = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Estado && c.EstadoRegistro)
            .Where(c =>
                EF.Functions.ILike(c.Nombre, $"%{valor}%") ||
                EF.Functions.ILike(c.Telefono, $"%{valor}%") ||
                (c.Dni != null && EF.Functions.ILike(c.Dni, $"%{valor}%")))
            .OrderBy(c => c.Nombre)
            .Take(Math.Clamp(limite, 1, 20))
            .Select(c => new
            {
                id = c.Id,
                nombre = c.Nombre,
                telefono = c.Telefono,
                dni = c.Dni,
                saldoBidones = c.SaldoBidones
            })
            .ToListAsync();

        return Json(clientes);
    }

    // ==================== T25: GUARDAR BALANCE ====================
    [HttpPost("Balancear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Balancear(BalancearBidonesViewModel model)
    {
        // 1. Verificar productoLocal
        var productoLocal = await _context.ProductosLocal
            .Include(pl => pl.Producto)
            .FirstOrDefaultAsync(pl =>
                pl.IdLocal == model.IdLocal &&
                pl.IdProducto == model.IdProducto &&
                pl.Estado &&
                pl.EstadoRegistro);

        if (productoLocal == null)
            return NotFound();

        if (!string.Equals(productoLocal.Producto.Categoria, "Bidón", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { ok = false, mensaje = "Solo se pueden balancear productos de categoría Bidón." });

        // ============ MODO PRODUCCIÓN (Aumentar / Descontar) ============
        if (string.Equals(model.TipoMovimiento, "Produccion", StringComparison.OrdinalIgnoreCase))
        {
            var cantidad = model.CantidadProduccion ?? 0;
            var accion = model.AccionProduccion ?? "Aumentar";

            if (cantidad <= 0)
                return BadRequest(new { ok = false, mensaje = "Ingresa una cantidad mayor a cero." });

            if (accion == "Aumentar")
            {
                productoLocal.Stock += cantidad;
            }
            else if (accion == "Descontar")
            {
                if (cantidad > productoLocal.Stock)
                    return BadRequest(new { ok = false, mensaje = $"No se puede descontar más del stock disponible ({productoLocal.Stock} unid.)." });

                productoLocal.Stock -= cantidad;
            }
            else
            {
                return BadRequest(new { ok = false, mensaje = "Acción de producción inválida." });
            }

            await _context.SaveChangesAsync();

            var signo = accion == "Aumentar" ? "+" : "-";

            return Json(new
            {
                ok = true,
                mensaje = $"PRODUCCIÓN ACTUALIZADA ({signo}{cantidad} unid.)",
                stockFinal = productoLocal.Stock
            });
        }

        // ============ MODO ENTREGA (solo afecta saldo del cliente, no toca stock) ============
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == model.IdCliente && c.Estado && c.EstadoRegistro);

        if (cliente == null)
            return BadRequest(new { ok = false, mensaje = "Seleccione un cliente válido." });

        var entregados = model.BidonesLlenosEntregados ?? 0;
        var devueltos = model.BidonesVaciosDevueltos ?? 0;

        if (entregados < 0 || devueltos < 0)
            return BadRequest(new { ok = false, mensaje = "Las cantidades no pueden ser negativas." });

        if (entregados == 0 && devueltos == 0)
            return BadRequest(new { ok = false, mensaje = "Ingresa al menos una cantidad mayor a cero." });

        // Actualizar solo el saldo del cliente (sin tocar stock)
        cliente.SaldoBidones += (entregados - devueltos);

        // Registrar movimiento (sin tocar stock)
        _context.MovimientosBidones.Add(new MovimientoBidones
        {
            IdCliente = cliente.Id,
            BidonesEntregados = entregados,
            BidonesDevueltos = devueltos,
            Saldo = cliente.SaldoBidones,
            FechaCreacion = DateTime.UtcNow,
            EstadoRegistro = true
        });

        await _context.SaveChangesAsync();

        var saldoTexto = cliente.SaldoBidones > 0
            ? $"Pendiente de devolver: {cliente.SaldoBidones}"
            : cliente.SaldoBidones < 0
                ? $"Bidones a favor: {Math.Abs(cliente.SaldoBidones)}"
                : "Sin bidones pendientes";

        return Json(new
        {
            ok = true,
            mensaje = "BALANCE GUARDADO CORRECTAMENTE",
            saldoFinal = cliente.SaldoBidones,
            saldoTexto
        });
    }
}