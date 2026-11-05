
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

        // Definir el rango de fechas
        var inicioPeriodo = fechaInicio.HasValue && fechaFin.HasValue
            ? DateOnly.FromDateTime(fechaInicio.Value)
            : new DateOnly(hoy.Year, hoy.Month, 1);

        var finPeriodo = fechaInicio.HasValue && fechaFin.HasValue
            ? DateOnly.FromDateTime(fechaFin.Value)
            : hoy;

        // Si las fechas son inválidas, usar el mes actual
        if (inicioPeriodo > finPeriodo)
        {
            inicioPeriodo = new DateOnly(hoy.Year, hoy.Month, 1);
            finPeriodo = hoy;
        }

        var inicioUtc = DateTime.SpecifyKind(
            inicioPeriodo.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        var finUtc = DateTime.SpecifyKind(
            finPeriodo.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        // Consultar pedidos del período
        var pedidosDelPeriodo = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro &&
                        p.FechaCreacion >= inicioUtc &&
                        p.FechaCreacion < finUtc)
            .ToListAsync();

        var ventasDelMes = pedidosDelPeriodo.Sum(p => p.Total);

        var pedidosDelDia = pedidosDelPeriodo.Count;

        // Consultar productos con stock bajo
        var productosStockBajo = await _context.Productos
            .AsNoTracking()
            .CountAsync(p => p.EstadoRegistro &&
                             p.Estado &&
                             p.StockActual <= p.StockMinimo);

        // Consultar deuda total del período
        var totalDeuda = await _context.Deudas
            .AsNoTracking()
            .Where(d => d.EstadoRegistro &&
                        d.Estado != EstadosDeuda.Pagada &&
                        d.FechaCreacion >= inicioUtc &&
                        d.FechaCreacion < finUtc)
            .SumAsync(d => (decimal?)d.Monto) ?? 0m;

        // Consultar deuda vencida del período
        var deudaVencida = await _context.Deudas
            .AsNoTracking()
            .Where(d => d.EstadoRegistro &&
                        d.Estado == EstadosDeuda.Vencida &&
                        d.FechaCreacion >= inicioUtc &&
                        d.FechaCreacion < finUtc)
            .SumAsync(d => (decimal?)d.Monto) ?? 0m;

        // Calcular índice de morosidad
        var indiceMorosidad = totalDeuda > 0
            ? (deudaVencida / totalDeuda) * 100
            : 0m;

        // Consultar ventas agrupadas por mes
        var ventasPorMes = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro &&
                        p.FechaCreacion >= inicioUtc &&
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

        var cantidadMeses =
            ((finPeriodo.Year - inicioPeriodo.Year) * 12)
            + finPeriodo.Month - inicioPeriodo.Month + 1;

        for (int i = 0; i < cantidadMeses; i++)
        {
            var mes = inicioPeriodo.AddMonths(i);

            mesesVentas.Add(
                mes.ToDateTime(TimeOnly.MinValue)
                    .ToString("MMM yyyy"));

            var ventaMes = ventasPorMes.FirstOrDefault(v =>
                v.Year == mes.Year &&
                v.Month == mes.Month);

            ventasUltimosSeisMeses.Add(
                ventaMes?.Total ?? 0m);
        }

        // Consultar clientes activos
        var clientesActivos = await _context.Clientes
            .AsNoTracking()
            .CountAsync(c => c.EstadoRegistro && c.Estado);

        // Consultar clientes nuevos del período
        var clientesNuevosDelMes = await _context.Clientes
            .AsNoTracking()
            .CountAsync(c => c.EstadoRegistro &&
                             c.FechaCreacion >= inicioUtc &&
                             c.FechaCreacion < finUtc);

        // Consultar clientes frecuentes del período
        var clientesFrecuentes = await _context.PedidosCliente
            .AsNoTracking()
            .Where(pc => pc.EstadoRegistro &&
                         pc.Pedido.EstadoRegistro &&
                         pc.Pedido.FechaCreacion >= inicioUtc &&
                         pc.Pedido.FechaCreacion < finUtc)
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
