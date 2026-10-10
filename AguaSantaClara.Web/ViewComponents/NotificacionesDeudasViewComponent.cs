using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.ViewComponents;

public class NotificacionesDeudasViewComponent : ViewComponent
{
    private readonly DeudaNotificacionService _deudaNotificacionService;

    public NotificacionesDeudasViewComponent(DeudaNotificacionService deudaNotificacionService) =>
        _deudaNotificacionService = deudaNotificacionService;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var fechaPeru = DateTime.UtcNow.AddHours(-5);
        var modelo = await _deudaNotificacionService.ObtenerAsync(DateOnly.FromDateTime(fechaPeru));
        return View(modelo);
    }
}
