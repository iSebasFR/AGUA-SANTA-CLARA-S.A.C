using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public class HistorialComprasService : IHistorialComprasService
{
    private readonly AppDbContext _context;

    public HistorialComprasService(AppDbContext context) => _context = context;

    public async Task<HistorialComprasViewModel> ObtenerAsync(long idCliente)
    {
        // Solo lectura: sin tracking y solo las columnas necesarias.
        var pedidos = await _context.Pedidos
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

        // HU-19 T27: productos comprados por este cliente en esos pedidos
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

        return new HistorialComprasViewModel
        {
            Pedidos = pedidos
                .Select(p => new PedidoCompraViewModel
                {
                    IdPedido = p.Id,
                    Fecha = p.FechaCreacion.AddHours(-5), // hora de Perú (UTC-5)
                    Monto = p.Monto,
                    Estado = p.Estado,
                    Productos = productosPorPedido.GetValueOrDefault(p.Id) ?? []
                })
                .ToList()
        };
    }
}