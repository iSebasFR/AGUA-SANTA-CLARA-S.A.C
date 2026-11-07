using System.Security.Claims; // CAMBIO 1: using nuevo
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services; // CAMBIO 2: using nuevo
using Microsoft.AspNetCore.Http; // CAMBIO 3: para DefaultHttpContext
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
                FechaVencimiento = DateTime.UtcNow.AddDays(-2),
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
            FechaPago = DateTime.UtcNow.AddDays(-2),
            Cliente = cliente,
            Pedido = pedidoConDeuda,
            MetodoPago = metodoEfectivo
        });
        await db.SaveChangesAsync();

        // CAMBIO 4: el controlador recibe el servicio y un usuario falso con rol Gerente
        var controller = new ClientesController(db, new HistorialComprasService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Role, "Gerente") }, "test"))
                }
            }
        };

        var result = await controller.Index(search: null, estado: null, id: cliente.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ClientesIndexViewModel>(view.Model);
        Assert.Equal(50m, model.HistorialFinanciero.DeudaTotal);
        Assert.Equal(20m, model.HistorialFinanciero.SaldoAFavor);
        Assert.Equal(2, model.HistorialFinanciero.CantidadDeudasActivas);
        Assert.True(model.HistorialFinanciero.TieneDeudasVencidas);
        Assert.Equal(4, model.HistorialFinanciero.Movimientos.Count);
        Assert.Contains(model.HistorialFinanciero.Movimientos, movimiento =>
            movimiento.IdPedido == pedidoConDeuda.Id
            && movimiento.Tipo == "Deuda"
            && movimiento.Estado == EstadosDeuda.Vencida);
        Assert.Contains(model.HistorialFinanciero.Movimientos, movimiento =>
            movimiento.Tipo == "Pago" && movimiento.MetodoPago == "Yape" && movimiento.Monto == 130m);
        Assert.Equal(
            model.HistorialFinanciero.Movimientos.OrderByDescending(movimiento => movimiento.Fecha)
                .Select(movimiento => movimiento.Fecha),
            model.HistorialFinanciero.Movimientos.Select(movimiento => movimiento.Fecha));

        var fechaPagoAntiguo = DateTime.UtcNow.AddDays(-2).Date;
        var filtrado = await controller.Index(
            search: null,
            estado: null,
            id: cliente.Id,
            fechaDesde: fechaPagoAntiguo,
            fechaHasta: fechaPagoAntiguo);
        var modeloFiltrado = Assert.IsType<ClientesIndexViewModel>(Assert.IsType<ViewResult>(filtrado).Model);
        var movimientoFiltrado = Assert.Single(modeloFiltrado.HistorialFinanciero.Movimientos);
        Assert.Equal("Pago", movimientoFiltrado.Tipo);
        Assert.Equal("Efectivo", movimientoFiltrado.MetodoPago);
        Assert.Equal(10m, movimientoFiltrado.Monto);
    }
}