using System.Reflection;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Tests;

public class RentabilidadControllerTests
{
    private sealed class ServicioFalso : IRentabilidadService
    {
        public DateOnly? Desde { get; private set; }
        public DateOnly? Hasta { get; private set; }

        public Task<RentabilidadViewModel> ObtenerRentabilidadAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
        {
            Desde = desde;
            Hasta = hasta;
            return Task.FromResult(new RentabilidadViewModel { FechaDesde = desde, FechaHasta = hasta });
        }
    }

    private static (RentabilidadController Controller, ServicioFalso Servicio) Crear(bool ajax = false)
    {
        var servicio = new ServicioFalso();
        var http = new DefaultHttpContext();
        if (ajax)
            http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var controller = new RentabilidadController(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        return (controller, servicio);
    }

    [Fact]
    public void Controller_SoloPermiteAdministradora()
    {
        var atributo = typeof(RentabilidadController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal("Administradora", atributo!.Roles);
    }

    [Fact]
    public async Task Index_SinFechas_UsaDesdeInicioDeMesHastaHoy()
    {
        var (controller, servicio) = Crear();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var resultado = await controller.Index(null, null, null, CancellationToken.None);

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.IsType<RentabilidadViewModel>(vista.Model);
        Assert.Equal(new DateOnly(hoy.Year, hoy.Month, 1), servicio.Desde);
        Assert.Equal(hoy, servicio.Hasta);
    }

    [Fact]
    public async Task Index_FechasInvertidas_SeIntercambian()
    {
        var (controller, servicio) = Crear();

        await controller.Index(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1), null, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 1), servicio.Desde);
        Assert.Equal(new DateOnly(2026, 10, 31), servicio.Hasta);
    }

    [Fact]
    public async Task Index_PeticionAjax_DevuelveJsonConElReporte()
    {
        var (controller, servicio) = Crear(ajax: true);

        var resultado = await controller.Index(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), null, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(resultado);
        var reporte = json.Value!.GetType().GetProperty("reporte")!.GetValue(json.Value);
        var vm = Assert.IsType<RentabilidadViewModel>(reporte);
        Assert.Equal(new DateOnly(2026, 10, 15), vm.FechaHasta);
        Assert.Equal(new DateOnly(2026, 10, 1), servicio.Desde);
    }

    [Fact]
    public async Task Index_FormatJson_DevuelveJson()
    {
        var (controller, _) = Crear();

        var resultado = await controller.Index(null, null, "json", CancellationToken.None);

        Assert.IsType<JsonResult>(resultado);
    }
}
