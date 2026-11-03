using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AguaSantaClara.Tests;

public class ReportesControllerTests
{
    private static AppDbContext CrearDbContext(string nombreBd)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nombreBd)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Ventas_FiltroHoy_RetornaDatosYTotalesCorrectos()
    {
        await using var db = CrearDbContext(Guid.NewGuid().ToString());

        var cliente = new Cliente { Id = 1, Nombre = "Juan Pérez", Telefono = "987654321", EstadoRegistro = true };
        var metodoEfectivo = new MetodoPago { Id = 1, Nombre = "Efectivo", EstadoRegistro = true };
        db.Clientes.Add(cliente);
        db.MetodosPago.Add(metodoEfectivo);

        var hoy = DateTime.UtcNow;
        var ayer = hoy.AddDays(-1);

        // Pedido Hoy con pago parcial
        var pedidoHoy = new Pedido
        {
            Id = 245,
            Total = 100m,
            Estado = EstadosPedido.PagoParcial,
            FechaCreacion = hoy,
            EstadoRegistro = true
        };
        pedidoHoy.Clientes.Add(new PedidoCliente { Id = 1, IdPedido = 245, IdCliente = 1, Cliente = cliente, EstadoRegistro = true });

        var pedidoAyer = new Pedido
        {
            Id = 244,
            Total = 150m,
            Estado = EstadosPedido.Pagado,
            FechaCreacion = ayer,
            EstadoRegistro = true
        };

        var pedidoInactivo = new Pedido
        {
            Id = 246,
            Total = 200m,
            Estado = EstadosPedido.Pagado,
            FechaCreacion = hoy,
            EstadoRegistro = false
        };

        db.Pedidos.AddRange(pedidoHoy, pedidoAyer, pedidoInactivo);

        // Pagos
        db.Pagos.AddRange(
            new Pago { Id = 1, IdPedido = 245, IdCliente = 1, IdMetodoPago = 1, Monto = 60m, FechaPago = hoy, EstadoRegistro = true, Cliente = cliente, MetodoPago = metodoEfectivo },
            new Pago { Id = 2, IdPedido = 244, IdCliente = 1, IdMetodoPago = 1, Monto = 150m, FechaPago = ayer, EstadoRegistro = true, Cliente = cliente, MetodoPago = metodoEfectivo },
            new Pago { Id = 3, IdPedido = 245, IdCliente = 1, IdMetodoPago = 1, Monto = 50m, FechaPago = hoy, EstadoRegistro = false, Cliente = cliente, MetodoPago = metodoEfectivo }
        );

        // Deudas: Pedido de 100m con pago de 60m deja saldo pendiente de 40m
        db.Deudas.AddRange(
            new Deuda { Id = 1, IdPedido = 245, IdCliente = 1, Monto = 100m, FechaCreacion = hoy, FechaVencimiento = hoy.AddDays(15), EstadoRegistro = true, Cliente = cliente, Pedido = pedidoHoy },
            new Deuda { Id = 2, IdPedido = 244, IdCliente = 1, Monto = 150m, FechaCreacion = ayer, FechaVencimiento = ayer.AddDays(15), EstadoRegistro = true, Cliente = cliente, Pedido = pedidoAyer },
            new Deuda { Id = 3, IdPedido = 245, IdCliente = 1, Monto = 300m, FechaCreacion = hoy, FechaVencimiento = hoy.AddDays(15), EstadoRegistro = false, Cliente = cliente, Pedido = pedidoHoy }
        );

        await db.SaveChangesAsync();

        var controller = new ReportesController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.Ventas(filtro: "hoy");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ReporteVentasViewModel>(viewResult.Model);

        Assert.Equal("hoy", model.FiltroRapido);
        Assert.Equal(100m, model.TotalVentas);
        Assert.Equal(60m, model.TotalPagos);
        Assert.Equal(40m, model.TotalDeudas); // Saldo pendiente: 100 - 60 = 40
        Assert.Equal(1, model.CantidadPedidos);
        Assert.Equal(1, model.CantidadPagos);
        Assert.Equal(1, model.CantidadDeudas);
        Assert.Single(model.Pedidos);
        Assert.Equal("#0245", model.Pedidos[0].Numero);
        Assert.Equal("Juan Pérez", model.Pedidos[0].Cliente);
        Assert.Single(model.Deudas);
        Assert.Equal(40m, model.Deudas[0].Monto);
    }

    [Fact]
    public async Task Ventas_PeticionAjaxJson_RetornaJsonConTotales()
    {
        await using var db = CrearDbContext(Guid.NewGuid().ToString());

        var cliente = new Cliente { Id = 1, Nombre = "María López", Telefono = "987654321", EstadoRegistro = true };
        var metodo = new MetodoPago { Id = 1, Nombre = "Yape", EstadoRegistro = true };
        db.Clientes.Add(cliente);
        db.MetodosPago.Add(metodo);

        var hoy = DateTime.UtcNow;
        var pedido = new Pedido { Id = 10, Total = 250m, FechaCreacion = hoy, EstadoRegistro = true };
        pedido.Clientes.Add(new PedidoCliente { Id = 10, IdPedido = 10, IdCliente = 1, Cliente = cliente, EstadoRegistro = true });
        db.Pedidos.Add(pedido);

        await db.SaveChangesAsync();

        var controller = new ReportesController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.Ventas(filtro: "hoy", format: "json");

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);
    }

    [Fact]
    public async Task Exportar_GeneraExcelCorrectamente()
    {
        await using var db = CrearDbContext(Guid.NewGuid().ToString());

        var cliente = new Cliente { Id = 1, Nombre = "Carlos Ruiz", Telefono = "987654321", EstadoRegistro = true };
        var metodo = new MetodoPago { Id = 1, Nombre = "Efectivo", EstadoRegistro = true };
        db.Clientes.Add(cliente);
        db.MetodosPago.Add(metodo);

        var hoy = DateTime.UtcNow;
        var pedido = new Pedido { Id = 15, Total = 120m, FechaCreacion = hoy, EstadoRegistro = true };
        pedido.Clientes.Add(new PedidoCliente { Id = 15, IdPedido = 15, IdCliente = 1, Cliente = cliente, EstadoRegistro = true });
        db.Pedidos.Add(pedido);

        await db.SaveChangesAsync();

        var controller = new ReportesController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.Exportar(filtro: "hoy", formato: "excel");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        Assert.True(fileResult.FileContents.Length > 0);
        Assert.Contains(".xlsx", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task Exportar_GeneraCsvCorrectamente()
    {
        await using var db = CrearDbContext(Guid.NewGuid().ToString());

        var cliente = new Cliente { Id = 1, Nombre = "Carlos Ruiz", Telefono = "987654321", EstadoRegistro = true };
        var metodo = new MetodoPago { Id = 1, Nombre = "Efectivo", EstadoRegistro = true };
        db.Clientes.Add(cliente);
        db.MetodosPago.Add(metodo);

        var hoy = DateTime.UtcNow;
        var pedido = new Pedido { Id = 15, Total = 120m, FechaCreacion = hoy, EstadoRegistro = true };
        pedido.Clientes.Add(new PedidoCliente { Id = 15, IdPedido = 15, IdCliente = 1, Cliente = cliente, EstadoRegistro = true });
        db.Pedidos.Add(pedido);

        await db.SaveChangesAsync();

        var controller = new ReportesController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.Exportar(filtro: "hoy", formato: "csv");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.True(fileResult.FileContents.Length > 0);
        Assert.Contains(".csv", fileResult.FileDownloadName);

        var csvText = Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Contains("REPORTE DE VENTAS", csvText);
        Assert.Contains("Carlos Ruiz", csvText);
    }
}
