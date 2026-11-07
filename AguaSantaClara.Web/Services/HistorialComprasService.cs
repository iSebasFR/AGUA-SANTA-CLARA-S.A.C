using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public class HistorialComprasService : IHistorialComprasService
{
    private readonly AppDbContext _context;

    // HU-19 T28: estados que cuentan para el resumen (se excluyen Pendiente y Con Incidencia)
    private static readonly string[] EstadosQueCuentan =
    {
        EstadosPedido.Enviado,
        EstadosPedido.Entregado,
        EstadosPedido.Pagado,
        EstadosPedido.PagoParcial
    };

    // HU-19 T31: "Cliente frecuente" = más de 3 pedidos en el período (es decir, 4 o más)
    private const int MinimoPedidosClienteFrecuente = 4;

    public HistorialComprasService(AppDbContext context) => _context = context;

    public async Task<HistorialComprasViewModel> ObtenerAsync(
        long idCliente,
        DateTime? desde = null,
        DateTime? hasta = null)
    {
        var resultado = new HistorialComprasViewModel
        {
            FechaDesde = desde?.Date,
            FechaHasta = hasta?.Date
        };

        // HU-19 T29: validación del rango de fechas
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
        {
            resultado.ErrorFiltro = "La fecha inicial no puede ser posterior a la fecha final.";
            return resultado;
        }

        // Solo lectura: sin tracking y solo las columnas necesarias.
        var todosLosPedidos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro
                && p.Clientes.Any(pc => pc.EstadoRegistro && pc.IdCliente == idCliente))
            .Select(p => new
            {
                p.Id,
                p.FechaCreacion,
                p.Estado,
                Monto = p.Clientes
                    .Where(pc => pc.EstadoRegistro && pc.IdCliente == idCliente)
                    .Sum(pc => pc.Subtotal)
            })
            .ToListAsync();

        // HU-19 T29: se filtra por la fecha de Perú (UTC-5), igual que se muestra en pantalla
        var pedidos = todosLosPedidos
            .Where(p =>
            {
                var fechaPeru = p.FechaCreacion.AddHours(-5).Date;
                return (!desde.HasValue || fechaPeru >= desde.Value.Date)
                    && (!hasta.HasValue || fechaPeru <= hasta.Value.Date);
            })
            .ToList();

        // HU-19 T27: productos comprados por este cliente en los pedidos del período
        var idsPedidos = pedidos.Select(p => p.Id).ToList();

        var detalles = await _context.DetallesPedido
            .AsNoTracking()
            .Where(d => d.EstadoRegistro
                && d.PedidoCliente.EstadoRegistro
                && d.PedidoCliente.IdCliente == idCliente
                && idsPedidos.Contains(d.PedidoCliente.IdPedido))
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.PedidoCliente.IdPedido,
                Producto = d.Producto.Nombre,
                d.Cantidad,
                d.PrecioUnitario,
                d.DescuentoMonto,
                d.Subtotal
            })
            .ToListAsync();

        var productosPorPedido = detalles
            .GroupBy(d => d.IdPedido)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new ProductoCompraViewModel
                {
                    Producto = d.Producto,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    DescuentoMonto = d.DescuentoMonto,
                    Subtotal = d.Subtotal
                }).ToList());

        // HU-19 T28: resumen calculado solo con los pedidos de estados válidos
        // (T29: ahora sobre el período filtrado)
        var pedidosQueCuentan = pedidos
            .Where(p => EstadosQueCuentan.Contains(p.Estado))
            .ToList();

        resultado.TotalGastado = pedidosQueCuentan.Sum(p => p.Monto);
        resultado.CantidadPedidos = pedidosQueCuentan.Count;

        // HU-19 T31: se calcula sobre los pedidos que cuentan, dentro del período filtrado
        resultado.EsClienteFrecuente = resultado.CantidadPedidos >= MinimoPedidosClienteFrecuente;

        resultado.FechaUltimaCompra = pedidosQueCuentan.Count > 0
            ? pedidosQueCuentan.Max(p => p.FechaCreacion).AddHours(-5) // hora de Perú (UTC-5)
            : null;
        resultado.Pedidos = pedidos
            // HU-19 T32: del más reciente al más antiguo (si empatan en fecha, el de mayor N° primero)
            .OrderByDescending(p => p.FechaCreacion)
            .ThenByDescending(p => p.Id)
            .Select(p => new PedidoCompraViewModel
            {
                IdPedido = p.Id,
                Fecha = p.FechaCreacion.AddHours(-5), // hora de Perú (UTC-5)
                Monto = p.Monto,
                Estado = p.Estado,
                Productos = productosPorPedido.GetValueOrDefault(p.Id) ?? []
            })
            .ToList();

        return resultado;
    }
}