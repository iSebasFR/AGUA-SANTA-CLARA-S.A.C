
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
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

    public async Task<IActionResult> Index()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var inicioUtc = DateTime.SpecifyKind(
            inicioMes.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

        var finUtc = DateTime.SpecifyKind(
            inicioMesSiguiente.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Local).ToUniversalTime();

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

        var modelo = new PanelIndicadoresViewModel
        {
            VentasDelMes = ventasDelMes
        };

        return View(modelo);
    }
}
