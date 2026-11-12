using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

// HU-21: controlador propio (no en ReportesController) porque solo depende del servicio y solo lo usa la Administradora.
[Authorize(Roles = "Administradora")]
public class RentabilidadController : Controller
{
    private readonly IRentabilidadService _rentabilidadService;

    public RentabilidadController(IRentabilidadService rentabilidadService) => _rentabilidadService = rentabilidadService;

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? fechaDesde, DateOnly? fechaHasta, string? format, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;
        if (desde > hasta)
            (desde, hasta) = (hasta, desde);

        var vm = await _rentabilidadService.ObtenerRentabilidadAsync(desde, hasta, ct);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, reporte = vm });
        }

        return View(vm);
    }
}
