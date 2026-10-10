using AguaSantaClara.Web.Models.Alertas;

namespace AguaSantaClara.Web.Services;

public interface IAlertasService
{
    Task<IReadOnlyList<NotificacionResponse>> ObtenerAsync(
        DateTimeOffset fechaDeteccion,
        CancellationToken cancellationToken = default);
}
