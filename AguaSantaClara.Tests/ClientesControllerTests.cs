using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class ClientesControllerTests
{
    [Fact]
    public async Task Index_CalculaResumenFinancieroConDeudasYPagosRegistrados()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var cliente = new Cliente { Id = 1, Nombre = "Ana Cliente", Telefono = "900000001" };
        var pedidoConSaldoAFavor = new Pedido { Id = 10, Total = 100m };
        var pedidoConDeuda = new Pedido { Id = 11, Total = 60m };
        var metodoYape = new MetodoPago { Id = 1, Nombre = "Yape" };
        var metodoEfectivo = new MetodoPago { Id = 2, Nombre = "Efectivo" };

        db.Clientes.Add(cliente);
        db.Pedidos.AddRange(pedidoConSaldoAFavor, pedidoConDeuda);
        db.MetodosPago.AddRange(metodoYape, metodoEfectivo);
        db.Deudas.AddRange(
            new Deuda
            {
                Id = 1,
                IdPedido = pedidoConSaldoAFavor.Id,
                IdCliente = cliente.Id,
                Monto = 100m,
                MontoPagadoAlRegistrar = 10m,
                FechaVencimiento = DateTime.UtcNow.AddDays(10),
                Cliente = cliente,
                Pedido = pedidoConSaldoAFavor
            },
            new Deuda
            {
                Id = 2,
                IdPedido = pedidoConDeuda.Id,
                IdCliente = cliente.Id,
                Monto = 60m,
                FechaVencimiento = DateTime.UtcNow.AddDays(-1),
                Cliente = cliente,
                Pedido = pedidoConDeuda
            });
        db.Pagos.Add(new Pago
        {
            Id = 1,
            IdPedido = pedidoConSaldoAFavor.Id,
            IdCliente = cliente.Id,
            IdMetodoPago = metodoYape.Id,
            Monto = 130m,
            Cliente = cliente,
            Pedido = pedidoConSaldoAFavor,
            MetodoPago = metodoYape
        });
        db.Pagos.Add(new Pago
        {
            Id = 2,
            IdPedido = pedidoConDeuda.Id,
            IdCliente = cliente.Id,
            IdMetodoPago = metodoEfectivo.Id,
            Monto = 10m,
            FechaPago = DateTime.UtcNow.AddDays(-1),
            Cliente = cliente,
            Pedido = pedidoConDeuda,
            MetodoPago = metodoEfectivo
        });
        await db.SaveChangesAsync();

        var controller = new ClientesController(db);

        var result = await controller.Index(search: null, estado: null, id: cliente.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ClientesIndexViewModel>(view.Model);
        Assert.Equal(50m, model.HistorialFinanciero.DeudaTotal);
        Assert.Equal(20m, model.HistorialFinanciero.SaldoAFavor);
        Assert.Equal(2, model.HistorialFinanciero.CantidadDeudasActivas);
        Assert.Equal(2, model.HistorialFinanciero.Deudas.Count);
        Assert.Contains(model.HistorialFinanciero.Deudas, deuda =>
            deuda.IdPedido == pedidoConDeuda.Id && deuda.Estado == EstadosDeuda.Vencida);
        Assert.Equal(2, model.HistorialFinanciero.Pagos.Count);
        Assert.Equal("Yape", model.HistorialFinanciero.Pagos[0].MetodoPago);
        Assert.Equal(130m, model.HistorialFinanciero.Pagos[0].Monto);
        Assert.Equal("Efectivo", model.HistorialFinanciero.Pagos[1].MetodoPago);
    }
}
