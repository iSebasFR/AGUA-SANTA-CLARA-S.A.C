using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Alertas;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public sealed class AlertasService(AppDbContext context) : IAlertasService
{
    public async Task<IReadOnlyList<NotificacionResponse>> ObtenerAsync(
        DateTimeOffset fechaDeteccion,
        CancellationToken cancellationToken = default)
    {
        var offset = fechaDeteccion.Offset;
        var hoy = DateTime.SpecifyKind(fechaDeteccion.Date, DateTimeKind.Unspecified);
        var deudasActivas = await context.Deudas
            .Where(deuda => deuda.EstadoRegistro && deuda.Estado != EstadosDeuda.Pagada)
            .ToListAsync(cancellationToken);
        var idPedidosConDeuda = deudasActivas.Select(deuda => deuda.IdPedido).Distinct().ToList();
        var idClientesConDeuda = deudasActivas.Select(deuda => deuda.IdCliente).Distinct().ToList();
        var pagosPorPedidoCliente = await context.Pagos
            .Where(pago => pago.EstadoRegistro
                && idPedidosConDeuda.Contains(pago.IdPedido)
                && idClientesConDeuda.Contains(pago.IdCliente))
            .GroupBy(pago => new { pago.IdPedido, pago.IdCliente })
            .Select(grupo => new
            {
                grupo.Key.IdPedido,
                grupo.Key.IdCliente,
                MontoPagado = grupo.Sum(pago => pago.Monto)
            })
            .ToDictionaryAsync(
                grupo => (grupo.IdPedido, grupo.IdCliente),
                grupo => grupo.MontoPagado,
                cancellationToken);

        var actualizarEstadoDeudas = false;
        foreach (var deuda in deudasActivas)
        {
            var montoPagado = pagosPorPedidoCliente.GetValueOrDefault((deuda.IdPedido, deuda.IdCliente));
            if (montoPagado >= deuda.Monto + deuda.MontoPagadoAlRegistrar)
            {
                actualizarEstadoDeudas |= deuda.Estado != EstadosDeuda.Pagada;
                deuda.Estado = EstadosDeuda.Pagada;
            }
            else if (deuda.FechaVencimiento < hoy)
            {
                actualizarEstadoDeudas |= deuda.Estado != EstadosDeuda.Vencida;
                deuda.Estado = EstadosDeuda.Vencida;
            }
        }

        if (actualizarEstadoDeudas)
            await context.SaveChangesAsync(cancellationToken);

        var alertas = new List<NotificacionResponse>();
        var productosConStockBajo = await context.ProductosLocal
            .Where(productoLocal => productoLocal.EstadoRegistro
                && productoLocal.Estado
                && productoLocal.Local.EstadoRegistro
                && productoLocal.Local.Estado
                && productoLocal.Producto.EstadoRegistro
                && productoLocal.Producto.Estado
                && productoLocal.Stock <= productoLocal.Producto.StockMinimo)
            .Select(productoLocal => new
            {
                NombreProducto = productoLocal.Producto.Nombre,
                NombreLocal = productoLocal.Local.Nombre,
                StockActual = productoLocal.Stock,
                StockMinimo = productoLocal.Producto.StockMinimo
            })
            .ToListAsync(cancellationToken);

        alertas.AddRange(productosConStockBajo.Select(producto => new NotificacionResponse(
            "Stock bajo de producto",
            $"{producto.NombreProducto} — {producto.NombreLocal}",
            $"Stock de {producto.NombreProducto} en {producto.NombreLocal}: {producto.StockActual} (mínimo: {producto.StockMinimo}).",
            producto.StockActual == 0 ? "Alta" : "Media",
            fechaDeteccion,
            producto.StockActual,
            producto.StockMinimo,
            null,
            null)));

        var insumosConStockBajo = await context.Insumos
            .Where(insumo => insumo.EstadoRegistro
                && insumo.Estado
                && insumo.StockActual <= insumo.StockMinimo)
            .Select(insumo => new
            {
                insumo.Nombre,
                insumo.StockActual,
                insumo.StockMinimo
            })
            .ToListAsync(cancellationToken);

        alertas.AddRange(insumosConStockBajo.Select(insumo => new NotificacionResponse(
            "Stock bajo de insumo",
            insumo.Nombre,
            $"Stock de insumo {insumo.Nombre}: {insumo.StockActual} (mínimo: {insumo.StockMinimo}).",
            insumo.StockActual == 0 ? "Alta" : "Media",
            fechaDeteccion,
            insumo.StockActual,
            insumo.StockMinimo,
            null,
            null)));

        var limiteVencimiento = DateTime.SpecifyKind(
            fechaDeteccion.Date.AddDays(3),
            DateTimeKind.Unspecified);
        var deudasProximas = await context.Deudas
            .Where(deuda => deuda.EstadoRegistro
                && deuda.Estado == EstadosDeuda.Pendiente
                && deuda.FechaVencimiento >= hoy
                && deuda.FechaVencimiento <= limiteVencimiento)
            .Select(deuda => new
            {
                deuda.IdPedido,
                deuda.IdCliente,
                NombreCliente = deuda.Cliente.Nombre,
                deuda.Monto,
                deuda.MontoPagadoAlRegistrar,
                deuda.FechaVencimiento
            })
            .ToListAsync(cancellationToken);

        alertas.AddRange(deudasProximas
            .Select(deuda => new
            {
                Deuda = deuda,
                MontoPendiente = deuda.Monto + deuda.MontoPagadoAlRegistrar
                    - pagosPorPedidoCliente.GetValueOrDefault((deuda.IdPedido, deuda.IdCliente))
            })
            .Where(item => item.MontoPendiente > 0)
            .Select(item => new NotificacionResponse(
            "Deuda próxima a vencer",
            $"{item.Deuda.NombreCliente} — Pedido N.° {item.Deuda.IdPedido}",
            $"La deuda de {item.Deuda.NombreCliente} por S/ {item.MontoPendiente:0.00} vence el {item.Deuda.FechaVencimiento:dd/MM/yyyy}.",
            item.Deuda.FechaVencimiento.Date == hoy ? "Alta" : "Media",
            fechaDeteccion,
            null,
            null,
            new DateTimeOffset(
                DateTime.SpecifyKind(item.Deuda.FechaVencimiento, DateTimeKind.Unspecified),
                offset),
            item.MontoPendiente)));

        return alertas;
    }
}
