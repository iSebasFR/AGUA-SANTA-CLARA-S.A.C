using AguaSantaClara.Web.Models.Alertas;
using AguaSantaClara.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AguaSantaClara.Tests;

public class AlertasBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReintentaTrasErrorYRegistraAlertasCuandoElJobSeEjecuta()
    {
        var llamadas = 0;
        var ejecucionCorrecta = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var alertas = new RecordingAlertasService((fecha, _) =>
        {
            if (Interlocked.Increment(ref llamadas) == 1)
                throw new InvalidOperationException("Error transitorio de prueba.");

            Assert.Equal(TimeSpan.Zero, fecha.Offset);
            ejecucionCorrecta.TrySetResult();
            return Task.FromResult<IReadOnlyList<NotificacionResponse>>(
            [
                new NotificacionResponse(
                    "Stock bajo de insumo",
                    "Tapa",
                    "Stock actual: 0; stock mínimo: 4.",
                    "Alta",
                    fecha,
                    0,
                    4,
                    null,
                    null)
            ]);
        });
        using var provider = CrearProvider(alertas);
        using var backgroundService = CrearServicio(provider);

        await backgroundService.StartAsync(CancellationToken.None);
        await ejecucionCorrecta.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Equal(2, Volatile.Read(ref llamadas));
    }

    [Fact]
    public async Task ExecuteAsync_SeDetieneCuandoLaConsultaSeCancelaDuranteElApagado()
    {
        var consultaIniciada = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var alertas = new RecordingAlertasService(async (_, cancellationToken) =>
        {
            consultaIniciada.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Array.Empty<NotificacionResponse>();
        });
        using var provider = CrearProvider(alertas);
        using var backgroundService = CrearServicio(provider);

        await backgroundService.StartAsync(CancellationToken.None);
        await consultaIniciada.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.Equal(1, alertas.Llamadas);
    }

    private static ServiceProvider CrearProvider(IAlertasService alertas) =>
        new ServiceCollection()
            .AddScoped(_ => alertas)
            .BuildServiceProvider();

    private static AlertasBackgroundService CrearServicio(IServiceProvider provider)
    {
        var ahora = DateTimeOffset.UtcNow;
        var opciones = Options.Create(new AlertasOptions
        {
            HoraEjecucion = ahora.Hour,
            MinutoEjecucion = ahora.Minute,
            UtcOffsetHoras = 0,
            FrecuenciaDias = 1,
            IntervaloRevisionSegundos = 1
        });

        return new AlertasBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            opciones,
            NullLogger<AlertasBackgroundService>.Instance);
    }

    private sealed class RecordingAlertasService(
        Func<DateTimeOffset, CancellationToken, Task<IReadOnlyList<NotificacionResponse>>> handler)
        : IAlertasService
    {
        private int _llamadas;

        public int Llamadas => Volatile.Read(ref _llamadas);

        public Task<IReadOnlyList<NotificacionResponse>> ObtenerAsync(
            DateTimeOffset fechaDeteccion,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _llamadas);
            return handler(fechaDeteccion, cancellationToken);
        }
    }
}
