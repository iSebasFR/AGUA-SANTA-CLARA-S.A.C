using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Alertas;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class AlertasServiceTests
{
    [Fact]
    public async Task ObtenerAsync_DevuelveAlertasDeStockPorLocalYDeudasProximasSinGuardarNotificaciones()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var cliente = new Cliente { Nombre = "Ana Torres", Telefono = "999000111" };
        var pedido = new Pedido { Estado = EstadosPedido.PagoParcial };
        var pedidoPagada = new Pedido { Estado = EstadosPedido.PagoParcial };
        var metodoPago = new MetodoPago { Nombre = "Efectivo" };
        var local = new Local { Nombre = "Local Central" };
        var producto = new Producto
        {
            Nombre = "Bidón",
            StockMinimo = 5,
            Estado = true
        };
        var deudaVencida = new Deuda
        {
            Cliente = cliente,
            Pedido = pedido,
            Monto = 30m,
            FechaVencimiento = new DateTime(2026, 10, 8),
            Estado = EstadosDeuda.Pendiente
        };
        db.ProductosLocal.AddRange(
            new ProductoLocal
            {
                Local = local,
                Producto = producto,
                Stock = 3,
                Estado = true
            },
            new ProductoLocal
            {
                Local = new Local { Nombre = "Local Norte" },
                Producto = producto,
                Stock = 6,
                Estado = true
            });
        db.Insumos.Add(new Insumo
        {
            Nombre = "Tapa",
            LineaProducto = "Envases",
            StockActual = 2,
            StockMinimo = 4
        });
        var deudaPagada = new Deuda
        {
            Cliente = cliente,
            Pedido = pedidoPagada,
            Monto = 5m,
            FechaVencimiento = new DateTime(2026, 10, 12),
            Estado = EstadosDeuda.Pendiente
        };
        db.Deudas.AddRange(
            deudaVencida,
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 20m,
                FechaVencimiento = new DateTime(2026, 10, 12),
                Estado = EstadosDeuda.Pendiente
            },
            deudaPagada);
        db.MetodosPago.Add(metodoPago);
        await db.SaveChangesAsync();
        db.Pagos.Add(new Pago
        {
            IdPedido = pedidoPagada.Id,
            IdCliente = cliente.Id,
            IdMetodoPago = metodoPago.Id,
            Monto = 5m
        });
        await db.SaveChangesAsync();

        var service = new AlertasService(db);
        var alerts = await service.ObtenerAsync(
            new DateTimeOffset(2026, 10, 9, 22, 0, 0, TimeSpan.FromHours(-5)));

        Assert.Equal(EstadosDeuda.Vencida, deudaVencida.Estado);
        Assert.Equal(EstadosDeuda.Pagada, deudaPagada.Estado);
        Assert.Equal(3, alerts.Count);
        Assert.Contains(alerts, alert =>
            alert.Tipo == "Stock bajo de producto"
            && alert.EntidadAfectada == "Bidón — Local Central"
            && alert.StockActual == 3
            && alert.StockMinimo == 5);
        Assert.Contains(alerts, alert =>
            alert.Tipo == "Stock bajo de insumo"
            && alert.EntidadAfectada == "Tapa"
            && alert.StockActual == 2
            && alert.StockMinimo == 4);
        Assert.Contains(alerts, alert =>
            alert.Tipo == "Deuda próxima a vencer"
            && alert.Monto == 20m
            && alert.FechaVencimiento == new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.FromHours(-5)));
        Assert.DoesNotContain(alerts, alert => alert.Monto == 5m);
        Assert.All(alerts, alert =>
        {
            Assert.NotEmpty(alert.Tipo);
            Assert.NotEmpty(alert.Mensaje);
            Assert.NotEmpty(alert.Prioridad);
            Assert.Equal(
                new DateTimeOffset(2026, 10, 9, 22, 0, 0, TimeSpan.FromHours(-5)),
                alert.FechaDeteccion);
        });
        Assert.Equal(3, await db.Deudas.CountAsync());
    }

    [Fact]
    public void EsDiaDeEjecucion_RespetaFrecuenciaYEvitaSegundaEjecucionElMismoDia()
    {
        var hoy = new DateOnly(2026, 10, 10);

        Assert.True(AlertasBackgroundService.EsDiaDeEjecucion(hoy, null, 1));
        Assert.False(AlertasBackgroundService.EsDiaDeEjecucion(hoy, hoy, 1));
        Assert.True(AlertasBackgroundService.EsDiaDeEjecucion(hoy.AddDays(1), hoy, 1));
        Assert.False(AlertasBackgroundService.EsDiaDeEjecucion(hoy.AddDays(1), hoy, 2));
    }
}
