using AguaSantaClara.Web.Models.Alertas;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers.Api;

[ApiController]
[Route("api/notificaciones")]
[Authorize(Roles = "Administradora,Vendedora")]
public sealed class NotificacionesController(
    IAlertasService alertasService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Returns transient alerts applicable to the current user's role.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificacionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificacionResponse>>> Obtener(
        CancellationToken cancellationToken)
    {
        var utcOffsetHours = configuration.GetValue<int?>("Alertas:UtcOffsetHoras") ?? -5;
        var ahora = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(utcOffsetHours));
        var notificaciones = await alertasService.ObtenerAsync(ahora, cancellationToken);

        var respuesta = User.IsInRole("Administradora")
            ? notificaciones.Where(n => n.Tipo.StartsWith("Stock bajo", StringComparison.Ordinal))
            : notificaciones.Where(n => n.Tipo == "Deuda próxima a vencer");

        return Ok(respuesta);
    }
}
