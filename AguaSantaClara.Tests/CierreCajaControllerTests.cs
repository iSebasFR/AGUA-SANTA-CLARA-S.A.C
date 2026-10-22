using System.Security.Claims;
using System.IO.Compression;
using System.Xml.Linq;
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

        db.CierresCaja.AddRange(
            new CierreCaja
            {
                IdVendedora = 20,
                Fecha = hoy.AddDays(-1),
                EstadoRegistro = true,
                Estado = EstadosCierreCaja.Cuadrado,
                Observacion = "Cierre anterior"
            },
            new CierreCaja
            {
                IdVendedora = 20,
                Fecha = hoy.AddDays(1),
                EstadoRegistro = true,
                Estado = EstadosCierreCaja.Cuadrado,
                Observacion = "Cierre posterior"
            });
        await db.SaveChangesAsync();

        var filtrado = Assert.IsType<ViewResult>(
            await controller.Index(hoy, hoy));
        var cierresFiltrados = Assert.IsAssignableFrom<IReadOnlyList<CierreCaja>>(filtrado.Model);
        Assert.Single(cierresFiltrados);
        Assert.Equal(hoy, cierresFiltrados[0].Fecha);

        var rangoInvertido = Assert.IsType<ViewResult>(
            await controller.Index(hoy.AddDays(1), hoy));
        var sinResultados = Assert.IsAssignableFrom<IReadOnlyList<CierreCaja>>(rangoInvertido.Model);
        Assert.Empty(sinResultados);
        Assert.NotNull(rangoInvertido.ViewData["ErrorFiltroFechas"]);

        var cierreOtraVendedora = new CierreCaja
        {
            IdVendedora = 21,
            Fecha = hoy,
            EstadoRegistro = true,
            Estado = EstadosCierreCaja.Cuadrado,
            Observacion = "Cierre de otra vendedora"
        };
        db.CierresCaja.Add(cierreOtraVendedora);
        await db.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await controller.Details(cierreOtraVendedora.Id));

        var archivo = Assert.IsType<FileContentResult>(await controller.Exportar());
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            archivo.ContentType);
        Assert.EndsWith(".xlsx", archivo.FileDownloadName);
        using var contenidoExcel = new MemoryStream(archivo.FileContents);
        using var libro = new ZipArchive(contenidoExcel, ZipArchiveMode.Read);
        var hoja = libro.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(hoja);
        using var lectorHoja = hoja!.Open();
        var xmlHoja = XDocument.Load(lectorHoja);
        var contenidoHoja = string.Join(
            " ",
            xmlHoja.Descendants().Where(elemento => elemento.Name.LocalName == "t"
                || elemento.Name.LocalName == "v").Select(elemento => elemento.Value));
        Assert.Contains("Fecha", contenidoHoja);
        Assert.Contains("Observaciones", contenidoHoja);
        Assert.Contains("Faltó S/ 1", contenidoHoja);
        Assert.DoesNotContain("Cierre de otra vendedora", contenidoHoja);

        var exportacionFiltrada = Assert.IsType<FileContentResult>(
            await controller.Exportar(hoy, hoy));
        using var contenidoExcelFiltrado = new MemoryStream(exportacionFiltrada.FileContents);
        using var libroFiltrado = new ZipArchive(contenidoExcelFiltrado, ZipArchiveMode.Read);
        using var lectorHojaFiltrada = libroFiltrado.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var contenidoHojaFiltrada = XDocument.Load(lectorHojaFiltrada);
        var textoHojaFiltrada = string.Join(
            " ",
            contenidoHojaFiltrada.Descendants().Where(elemento => elemento.Name.LocalName == "t"
                || elemento.Name.LocalName == "v").Select(elemento => elemento.Value));
        Assert.Contains("Faltó S/ 1", textoHojaFiltrada);
        Assert.DoesNotContain("Cierre anterior", textoHojaFiltrada);
        Assert.DoesNotContain("Cierre posterior", textoHojaFiltrada);

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
