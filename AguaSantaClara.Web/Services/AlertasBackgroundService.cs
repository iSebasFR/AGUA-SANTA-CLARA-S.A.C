using AguaSantaClara.Web.Models.Alertas;
using Microsoft.Extensions.Options;

namespace AguaSantaClara.Web.Services;

public sealed class AlertasBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<AlertasOptions> options,
    ILogger<AlertasBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuracion = options.Value;
        var ultimoDiaEjecutado = (DateOnly?)null;
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(configuracion.IntervaloRevisionSegundos));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var ahora = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(configuracion.UtcOffsetHoras));
            var hoy = DateOnly.FromDateTime(ahora.DateTime);
            var horaProgramada = new TimeOnly(configuracion.HoraEjecucion, configuracion.MinutoEjecucion);

            if (TimeOnly.FromDateTime(ahora.DateTime) < horaProgramada
                || !EsDiaDeEjecucion(hoy, ultimoDiaEjecutado, configuracion.FrecuenciaDias))
                continue;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var alertasService = scope.ServiceProvider.GetRequiredService<IAlertasService>();
                var notificaciones = await alertasService.ObtenerAsync(ahora, stoppingToken);

                logger.LogInformation(
                    "Job nocturno de alertas ejecutado a {FechaDeteccion}. Encontró {CantidadAlertas} alertas: {Productos} productos por local, {Insumos} insumos y {Deudas} deudas próximas a vencer.",
                    ahora,
                    notificaciones.Count,
                    notificaciones.Count(n => n.Tipo == "Stock bajo de producto"),
                    notificaciones.Count(n => n.Tipo == "Stock bajo de insumo"),
                    notificaciones.Count(n => n.Tipo == "Deuda próxima a vencer"));

                foreach (var notificacion in notificaciones)
                {
                    logger.LogWarning(
                        "Alerta {Tipo} ({Prioridad}) para {EntidadAfectada}: {Mensaje}",
                        notificacion.Tipo,
                        notificacion.Prioridad,
                        notificacion.EntidadAfectada,
                        notificacion.Mensaje);
                }

                ultimoDiaEjecutado = hoy;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falló la ejecución del job nocturno de alertas para {Fecha}.", hoy);
            }
        }
    }

    public static bool EsDiaDeEjecucion(DateOnly hoy, DateOnly? ultimoDiaEjecutado, int frecuenciaDias) =>
        ultimoDiaEjecutado is null
        || hoy.DayNumber - ultimoDiaEjecutado.Value.DayNumber >= frecuenciaDias;
}
