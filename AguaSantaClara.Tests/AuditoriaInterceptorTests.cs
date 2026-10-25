using System.Security.Claims;
using System.Text.Json;
using AguaSantaClara.Web.Auditoria;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class AuditoriaInterceptorTests
{
    private static AppDbContext CrearContexto(string? usuario = "admin", long idUsuario = 7)
    {
        var accessor = new HttpContextAccessor();
        if (usuario is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, usuario), new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString())],
                "test");
            accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditoriaInterceptor(accessor))
            .Options;

        return new AppDbContext(options);
    }

    private static Insumo NuevoInsumo() => new()
    {
        Nombre = "Tapas", Costo = 1.5m, LineaProducto = "Envases", StockActual = 10, StockMinimo = 2
    };

    [Fact]
    public async Task Insertar_RegistraUsuarioEntidadAccionFechaYValoresNuevos()
    {
        using var db = CrearContexto();
        var insumo = NuevoInsumo();
        db.Insumos.Add(insumo);

        await db.SaveChangesAsync();

        var auditoria = Assert.Single(db.AuditoriasCambio);
        Assert.Equal(AccionAuditoria.Insertar, auditoria.Accion);
        Assert.Equal(nameof(Insumo), auditoria.Entidad);
        Assert.Equal(insumo.Id.ToString(), auditoria.IdRegistro);
        Assert.Equal("admin", auditoria.NombreUsuario);
        Assert.Equal(7, auditoria.IdUsuario);
        Assert.True(auditoria.Fecha > DateTime.UtcNow.AddMinutes(-1));
        Assert.Null(auditoria.ValoresAnteriores);
        Assert.Contains("Tapas", auditoria.ValoresNuevos);
    }

    [Fact]
    public async Task Editar_RegistraSoloCamposCambiadosConValorAnteriorYNuevo()
    {
        using var db = CrearContexto();
        var insumo = NuevoInsumo();
        db.Insumos.Add(insumo);
        await db.SaveChangesAsync();

        insumo.StockActual = 25;
        await db.SaveChangesAsync();

        var auditoria = db.AuditoriasCambio.Single(a => a.Accion == AccionAuditoria.Editar);
        var anteriores = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(auditoria.ValoresAnteriores!)!;
        var nuevos = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(auditoria.ValoresNuevos!)!;
        Assert.Equal(10, anteriores["StockActual"].GetInt32());
        Assert.Equal(25, nuevos["StockActual"].GetInt32());
        Assert.Single(nuevos);
    }

    [Fact]
    public async Task BajaLogica_SeRegistraComoEliminar()
    {
        using var db = CrearContexto();
        var insumo = NuevoInsumo();
        db.Insumos.Add(insumo);
        await db.SaveChangesAsync();

        insumo.EstadoRegistro = false;
        await db.SaveChangesAsync();

        Assert.Single(db.AuditoriasCambio.Where(a => a.Accion == AccionAuditoria.Eliminar));
    }

    [Fact]
    public async Task EliminarFisico_RegistraValoresAnteriores()
    {
        using var db = CrearContexto();
        var insumo = NuevoInsumo();
        db.Insumos.Add(insumo);
        await db.SaveChangesAsync();

        db.Insumos.Remove(insumo);
        await db.SaveChangesAsync();

        var auditoria = db.AuditoriasCambio.Single(a => a.Accion == AccionAuditoria.Eliminar);
        Assert.Contains("Tapas", auditoria.ValoresAnteriores);
        Assert.Null(auditoria.ValoresNuevos);
    }

    [Fact]
    public async Task SinSesion_RegistraComoSistema()
    {
        using var db = CrearContexto(usuario: null);
        db.Insumos.Add(NuevoInsumo());

        await db.SaveChangesAsync();

        var auditoria = Assert.Single(db.AuditoriasCambio);
        Assert.Equal("Sistema", auditoria.NombreUsuario);
        Assert.Null(auditoria.IdUsuario);
    }

    [Fact]
    public async Task SaveChangesSincrono_TambienAudita()
    {
        using var db = CrearContexto();
        db.Insumos.Add(NuevoInsumo());

        db.SaveChanges();

        Assert.Single(db.AuditoriasCambio);
    }

    [Fact]
    public async Task NoAuditaCamposSensibles()
    {
        using var db = CrearContexto();
        var rol = new Rol { Name = "Vendedora", Codigo = "VENDEDORA" };
        db.Roles.Add(rol);
        await db.SaveChangesAsync();
        db.Users.Add(new Usuario
        {
            UserName = "ana", Nombres = "Ana", Apellidos = "T", IdRol = rol.Id, PasswordHash = "SECRETO-HASH"
        });

        await db.SaveChangesAsync();

        var auditoria = db.AuditoriasCambio.Single(a => a.Entidad == nameof(Usuario));
        Assert.DoesNotContain("SECRETO-HASH", auditoria.ValoresNuevos);
    }

    [Fact]
    public async Task ServicioConsulta_FiltraPorEntidadYAccion()
    {
        using var db = CrearContexto();
        var insumo = NuevoInsumo();
        db.Insumos.Add(insumo);
        await db.SaveChangesAsync();
        insumo.StockActual = 1;
        await db.SaveChangesAsync();
        db.Locales.Add(new Local { Nombre = "Central" });
        await db.SaveChangesAsync();

        var servicio = new AuditoriaService(db);
        var resultado = await servicio.ConsultarAsync(new FiltroAuditoria(Entidad: nameof(Insumo), Accion: AccionAuditoria.Editar));

        Assert.Equal(1, resultado.Total);
        Assert.Equal(3, (await servicio.ConsultarAsync(new FiltroAuditoria())).Total);
    }

    [Fact]
    public async Task ServicioConsulta_FiltraPorUsuarioYFechas()
    {
        using var db = CrearContexto();
        db.Insumos.Add(NuevoInsumo());
        await db.SaveChangesAsync();
        var servicio = new AuditoriaService(db);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var coincide = await servicio.ConsultarAsync(new FiltroAuditoria(Usuario: "ADM", Desde: hoy.AddDays(-1), Hasta: hoy));
        var otroUsuario = await servicio.ConsultarAsync(new FiltroAuditoria(Usuario: "otro"));
        var fueraDeRango = await servicio.ConsultarAsync(new FiltroAuditoria(Hasta: hoy.AddDays(-2)));
        var posterior = await servicio.ConsultarAsync(new FiltroAuditoria(Desde: hoy.AddDays(1)));

        Assert.Equal(1, coincide.Total);
        Assert.Equal(0, otroUsuario.Total);
        Assert.Equal(0, fueraDeRango.Total);
        Assert.Equal(0, posterior.Total);
    }

    [Fact]
    public async Task ServicioConsulta_Pagina()
    {
        using var db = CrearContexto();
        for (var i = 0; i < 3; i++) db.Insumos.Add(NuevoInsumo());
        await db.SaveChangesAsync();
        var servicio = new AuditoriaService(db);

        var resultado = await servicio.ConsultarAsync(new FiltroAuditoria(), pagina: 2, tamanoPagina: 2);

        Assert.Single(resultado.Items);
        Assert.Equal(2, resultado.TotalPaginas);
    }

    [Fact]
    public async Task ServicioObtener_DevuelveRegistroOEntidadesDistintas()
    {
        using var db = CrearContexto();
        db.Insumos.Add(NuevoInsumo());
        db.Locales.Add(new Local { Nombre = "Central" });
        await db.SaveChangesAsync();
        var servicio = new AuditoriaService(db);
        var id = db.AuditoriasCambio.First().Id;

        Assert.NotNull(await servicio.ObtenerAsync(id));
        Assert.Null(await servicio.ObtenerAsync(-1));
        Assert.Equal(new[] { nameof(Insumo), nameof(Local) }, await servicio.ObtenerEntidadesAsync());
    }
}
