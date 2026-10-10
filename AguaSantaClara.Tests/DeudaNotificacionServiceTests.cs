using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class DeudaNotificacionServiceTests
{
    [Fact]
    public async Task ObtenerAsync_MarcaDeudasVencidasYNotificaSoloPendientesEnLosProximosTresDias()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var cliente = new Cliente
        {
            Nombre = "Ana Torres",
            Telefono = "999000111"
        };
        var pedido = new Pedido { Estado = EstadosPedido.PagoParcial };
        db.Deudas.AddRange(
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 30m,
                FechaVencimiento = new DateTime(2026, 10, 8),
                Estado = EstadosDeuda.Pendiente
            },
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 20m,
                FechaVencimiento = new DateTime(2026, 10, 9),
                Estado = EstadosDeuda.Pendiente
            },
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 10m,
                FechaVencimiento = new DateTime(2026, 10, 12),
                Estado = EstadosDeuda.Pendiente
            },
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 5m,
                FechaVencimiento = new DateTime(2026, 10, 13),
                Estado = EstadosDeuda.Pendiente
            },
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 4m,
                FechaVencimiento = new DateTime(2026, 10, 10),
                Estado = EstadosDeuda.Pagada
            },
            new Deuda
            {
                Cliente = cliente,
                Pedido = pedido,
                Monto = 3m,
                FechaVencimiento = new DateTime(2026, 10, 10),
                Estado = EstadosDeuda.Pendiente,
                EstadoRegistro = false
            });
        await db.SaveChangesAsync();

        var servicio = new DeudaNotificacionService(db);
        var resultado = await servicio.ObtenerAsync(new DateOnly(2026, 10, 9));

        Assert.Equal(EstadosDeuda.Vencida, (await db.Deudas
            .SingleAsync(d => d.Monto == 30m)).Estado);
        Assert.Equal(2, resultado.Notificaciones.Count);
        Assert.All(resultado.Notificaciones, notificacion =>
        {
            Assert.Equal(cliente.Nombre, notificacion.NombreCliente);
            Assert.InRange(
                DateOnly.FromDateTime(notificacion.FechaVencimiento),
                new DateOnly(2026, 10, 9),
                new DateOnly(2026, 10, 12));
        });
        Assert.DoesNotContain(resultado.Notificaciones, notificacion => notificacion.Monto == 4m);
        Assert.DoesNotContain(resultado.Notificaciones, notificacion => notificacion.Monto == 3m);
        Assert.DoesNotContain(resultado.Notificaciones, notificacion => notificacion.Monto == 5m);
    }
}
