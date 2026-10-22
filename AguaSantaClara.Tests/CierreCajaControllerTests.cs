using System.Security.Claims;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class CierreCajaControllerTests
{
    [Fact]
    public async Task Create_CalculaMontosGuardaDiferenciaYRechazaDuplicado()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var rol = new Rol { Id = 1, Name = "Vendedora", Codigo = "VEN" };
        db.Roles.Add(rol);
        db.Usuarios.Add(new Usuario
        {
            Id = 20,
            IdRol = rol.Id,
            Rol = rol,
            UserName = "vendedora",
            Nombres = "Ana",
            Apellidos = "Torres",
            EstadoRegistro = true
        });

        var efectivo = new MetodoPago { Id = 1, Nombre = "Efectivo" };
        var yape = new MetodoPago { Id = 2, Nombre = "Yape" };
        var transferencia = new MetodoPago { Id = 3, Nombre = "Transferencia Bancaria" };
        db.MetodosPago.AddRange(efectivo, yape, transferencia);
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "Cliente", Telefono = "999999999" });
        db.Pedidos.Add(new Pedido { Id = 1, Estado = EstadosPedido.Pagado, Total = 164m });

        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var horaPago = DateTime.SpecifyKind(
            hoy.ToDateTime(new TimeOnly(12, 0)),
            DateTimeKind.Local).ToUniversalTime();
        db.Pagos.AddRange(
            CrearPago(1, 1, efectivo, 40m, horaPago),
            CrearPago(2, 1, yape, 25m, horaPago),
            CrearPago(3, 1, transferencia, 99m, horaPago),
            CrearPago(4, 1, yape, 50m, horaPago, estadoRegistro: false),
            CrearPago(5, 1, efectivo, 100m, horaPago.AddDays(-1)));
        await db.SaveChangesAsync();

        var controller = CrearController(db);
        var modelo = new CierreCajaInputViewModel
        {
            Fecha = hoy,
            EfectivoRecibido = 40m,
            YapeRecibido = 24m,
            GastosDelDia = 2m,
            Observacion = "Faltó S/ 1"
        };

        var resultado = Assert.IsType<JsonResult>(await controller.Create(modelo));
        var guardado = await db.CierresCaja.SingleAsync();

        Assert.Equal(40m, guardado.EfectivoEsperado);
        Assert.Equal(25m, guardado.YapeEsperado);
        Assert.Equal(-1m, guardado.Diferencia);
        Assert.Equal(EstadosCierreCaja.ConDiferencia, guardado.Estado);
        Assert.Equal("Faltó S/ 1", guardado.Observacion);
        Assert.NotNull(resultado.Value);

        var listado = Assert.IsType<ViewResult>(await controller.Index());
        var cierres = Assert.IsAssignableFrom<IReadOnlyList<CierreCaja>>(listado.Model);
        Assert.Single(cierres);

        var detalle = Assert.IsType<ViewResult>(await controller.Details(guardado.Id));
        var cierreDetalle = Assert.IsType<CierreCaja>(detalle.Model);
        Assert.Equal(guardado.Id, cierreDetalle.Id);
        Assert.Equal("Faltó S/ 1", cierreDetalle.Observacion);

        db.CierresCaja.Add(new CierreCaja
        {
            IdVendedora = 21,
            Fecha = hoy,
            EstadoRegistro = true,
            Estado = EstadosCierreCaja.Cuadrado
        });
        await db.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await controller.Details(2));

        var duplicado = Assert.IsType<ConflictObjectResult>(await controller.Create(modelo));
        Assert.Equal(409, duplicado.StatusCode);
    }

    private static Pago CrearPago(
        long id,
        long idCliente,
        MetodoPago metodoPago,
        decimal monto,
        DateTime fechaPago,
        bool estadoRegistro = true) =>
        new()
        {
            Id = id,
            IdPedido = 1,
            IdCliente = idCliente,
            IdMetodoPago = metodoPago.Id,
            MetodoPago = metodoPago,
            Monto = monto,
            FechaPago = fechaPago,
            EstadoRegistro = estadoRegistro
        };

    private static CierreCajaController CrearController(AppDbContext db)
    {
        var controller = new CierreCajaController(db);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "20")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        return controller;
    }
}
