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
        var metodoPago = new MetodoPago { Id = 1, Nombre = "Yape" };

        db.Clientes.Add(cliente);
        db.Pedidos.AddRange(pedidoConSaldoAFavor, pedidoConDeuda);
        db.MetodosPago.Add(metodoPago);
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
                FechaVencimiento = DateTime.UtcNow.AddDays(10),
                Cliente = cliente,
                Pedido = pedidoConDeuda
            });
        db.Pagos.Add(new Pago
        {
            Id = 1,
            IdPedido = pedidoConSaldoAFavor.Id,
            IdCliente = cliente.Id,
            IdMetodoPago = metodoPago.Id,
            Monto = 130m,
            Cliente = cliente,
            Pedido = pedidoConSaldoAFavor,
            MetodoPago = metodoPago
        });
        await db.SaveChangesAsync();

        var controller = new ClientesController(db);

        var result = await controller.Index(search: null, estado: null, id: cliente.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ClientesIndexViewModel>(view.Model);
        Assert.Equal(60m, model.HistorialFinanciero.DeudaTotal);
        Assert.Equal(20m, model.HistorialFinanciero.SaldoAFavor);
        Assert.Equal(2, model.HistorialFinanciero.CantidadDeudasActivas);
    }
}
