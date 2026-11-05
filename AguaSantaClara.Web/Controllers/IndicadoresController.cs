
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Gerente")]
public class IndicadoresController : Controller
{
    private readonly AppDbContext _context;

    public IndicadoresController(AppDbContext context)
    {
        _context = context;
    }
public async Task<IActionResult> Index(
    DateTime? fechaInicio,
    DateTime? fechaFin)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // Fechas del mes actual
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var inicioUtc = DateTime.SpecifyKind(
            inicioMes.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        var finUtc = DateTime.SpecifyKind(
            inicioMesSiguiente.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        // Consultar pedidos del mes
        var pedidosDelMes = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro &&
                        p.FechaCreacion >= inicioUtc &&
                        p.FechaCreacion < finUtc)
            .Select(p => new
            {
                p.Id,
                p.FechaCreacion,
                p.Total,
                p.Estado
            })
            .ToListAsync();

        var ventasDelMes = pedidosDelMes.Sum(p => p.Total);

        // Consultar productos con stock bajo
        var productosStockBajo = await _context.Productos
            .AsNoTracking()
            .CountAsync(p => p.EstadoRegistro &&
                             p.Estado &&
                             p.StockActual <= p.StockMinimo);

        // Consultar deuda total activa no pagada
        var totalDeuda = await _context.Deudas
            .AsNoTracking()
            .Where(d => d.EstadoRegistro &&
                        d.Estado != EstadosDeuda.Pagada)
            .SumAsync(d => (decimal?)d.Monto) ?? 0m;

        // Consultar deuda vencida
        var deudaVencida = await _context.Deudas
            .AsNoTracking()
            .Where(d => d.EstadoRegistro &&
                        d.Estado == EstadosDeuda.Vencida)
            .SumAsync(d => (decimal?)d.Monto) ?? 0m;

        // Calcular índice de morosidad
        var indiceMorosidad = totalDeuda > 0
            ? (deudaVencida / totalDeuda) * 100
            : 0m;

        // Fechas del día actual
        var inicioDia = DateTime.SpecifyKind(
            hoy.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        var inicioDiaSiguiente = DateTime.SpecifyKind(
            hoy.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        // Consultar pedidos del día
        var pedidosDelDia = await _context.Pedidos
            .AsNoTracking()
            .CountAsync(p => p.EstadoRegistro &&
                             p.FechaCreacion >= inicioDia &&
                             p.FechaCreacion < inicioDiaSiguiente);

        // Consultar ventas de los últimos seis meses
        var inicioPeriodo = new DateOnly(
            hoy.Year,
            hoy.Month,
            1).AddMonths(-5);

        var inicioPeriodoUtc = DateTime.SpecifyKind(
            inicioPeriodo.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        var ventasPorMes = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro &&
                        p.FechaCreacion >= inicioPeriodoUtc &&
                        p.FechaCreacion < finUtc)
            .GroupBy(p => new
            {
                p.FechaCreacion.Year,
                p.FechaCreacion.Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(p => p.Total)
            })
            .ToListAsync();

        var mesesVentas = new List<string>();
        var ventasUltimosSeisMeses = new List<decimal>();

        for (int i = 0; i < 6; i++)
        {
            var mes = inicioPeriodo.AddMonths(i);

            mesesVentas.Add(
                mes.ToDateTime(TimeOnly.MinValue)
                    .ToString("MMM yyyy"));

            var ventaMes = ventasPorMes
                .FirstOrDefault(v =>
                    v.Year == mes.Year &&
                    v.Month == mes.Month);

            ventasUltimosSeisMeses.Add(
                ventaMes?.Total ?? 0m);
        }
        
        // Consultar clientes activos
        var clientesActivos = await _context.Clientes
            .AsNoTracking()
            .CountAsync(c => c.EstadoRegistro && c.Estado);

        // Consultar clientes nuevos del mes
        var clientesNuevosDelMes = await _context.Clientes
            .AsNoTracking()
            .CountAsync(c => c.EstadoRegistro &&
                            c.FechaCreacion >= inicioUtc &&
                            c.FechaCreacion < finUtc);

// Consultar clientes frecuentes
var clientesFrecuentes = await _context.PedidosCliente
    .AsNoTracking()
    .Where(pc => pc.EstadoRegistro &&
                 pc.Pedido.EstadoRegistro)
    .GroupBy(pc => pc.IdCliente)
    .CountAsync(g => g.Count() >= 5);

        // Enviar indicadores a la vista
        var modelo = new PanelIndicadoresViewModel
        {
            VentasDelMes = ventasDelMes,
            PedidosDelDia = pedidosDelDia,
            IndiceMorosidad = indiceMorosidad,
            ProductosStockBajo = productosStockBajo,

            MesesVentas = mesesVentas,
            VentasUltimosSeisMeses = ventasUltimosSeisMeses,

            ClientesActivos = clientesActivos,
            ClientesNuevosDelMes = clientesNuevosDelMes,
            ClientesFrecuentes = clientesFrecuentes
        };

        return View(modelo);
    }
}
