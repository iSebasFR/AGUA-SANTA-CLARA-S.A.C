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

        return new HistorialComprasViewModel
        {
            Pedidos = pedidos
                .Select(p => new PedidoCompraViewModel
                {
                    IdPedido = p.Id,
                    Fecha = p.FechaCreacion.AddHours(-5), // hora de Perú (UTC-5)
                    Monto = p.Monto,
                    Estado = p.Estado
                })
                .ToList()
        };
    }
}