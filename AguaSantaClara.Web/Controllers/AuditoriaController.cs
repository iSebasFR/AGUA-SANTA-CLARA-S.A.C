using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

// HT-12: solo la Administradora puede consultar la auditoría.
[Authorize(Roles = "Administradora")]
public class AuditoriaController : Controller
{
    private readonly IAuditoriaService _auditoriaService;

    public AuditoriaController(IAuditoriaService auditoriaService)
    {
        _auditoriaService = auditoriaService;
    }

    public async Task<IActionResult> Index(
        string? entidad, string? accion, string? usuario,
        DateOnly? desde, DateOnly? hasta, int pagina = 1)
    {
        var filtro = new FiltroAuditoria(entidad, accion, usuario, desde, hasta);
        ViewBag.Filtro = filtro;
        ViewBag.Entidades = await _auditoriaService.ObtenerEntidadesAsync();

        return View(await _auditoriaService.ConsultarAsync(filtro, pagina));
    }

    public async Task<IActionResult> Detalle(long id)
    {
        var cambio = await _auditoriaService.ObtenerAsync(id);
        if (cambio is null) return NotFound();
        return View(cambio);
    }
}
