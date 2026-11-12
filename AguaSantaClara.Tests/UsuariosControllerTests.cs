using AguaSantaClara.Tests.Soporte;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

[Collection(PostgresCollection.Nombre)]
public class UsuariosControllerTests
{
    private static UsuariosController CrearControlador(EntornoIdentity entorno) =>
        ControladorFactory.Preparar(new UsuariosController(entorno.Db, entorno.UserManager));

    private static CrearUsuarioViewModel ModeloCrear(long? idRol, string userName = "jperez", string password = "Clave1234") => new()
    {
        Nombres = " Juan ",
        Apellidos = " Pérez ",
        UserName = $" {userName} ",
        Password = password,
        IdRol = idRol
    };

    // ---------- Index / filtros ----------

    [Fact]
    public async Task Index_SinFiltros_OcultaGerenteYOrdenaPorApellido()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente, apellidos: "Aaa");
        await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora, apellidos: "Zeta");
        await entorno.CrearUsuarioAsync("admin", entorno.RolAdmin, apellidos: "Beta");

        var resultado = await CrearControlador(entorno).Index(null, null, null);

        var modelo = Assert.IsType<UsuariosIndexViewModel>(Assert.IsType<ViewResult>(resultado).Model);
        Assert.Equal(new[] { "admin", "vend" }, modelo.Usuarios.Select(u => u.UserName));
        Assert.DoesNotContain(modelo.Roles, r => r.Value == entorno.RolGerente.Id.ToString());
        Assert.Contains(modelo.Roles, r => r.Text == "Administrador");
        Assert.Contains(modelo.Roles, r => r.Text == "Vendedor");
    }

    [Fact]
    public async Task Index_FiltraPorRolYEstado()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("admin1", entorno.RolAdmin);
        await entorno.CrearUsuarioAsync("vend1", entorno.RolVendedora);
        await entorno.CrearUsuarioAsync("vend2", entorno.RolVendedora, estado: false);
        var controller = CrearControlador(entorno);

        var porRol = Assert.IsType<UsuariosIndexViewModel>(Assert.IsType<ViewResult>(
            await controller.Index(null, entorno.RolVendedora.Id, null)).Model);
        Assert.Equal(new[] { "vend1", "vend2" }, porRol.Usuarios.Select(u => u.UserName).OrderBy(x => x));
        Assert.Equal(entorno.RolVendedora.Id, porRol.IdRolFiltro);

        var porRolYEstado = Assert.IsType<UsuariosIndexViewModel>(Assert.IsType<ViewResult>(
            await controller.Index(null, entorno.RolVendedora.Id, false)).Model);
        Assert.Equal("vend2", Assert.Single(porRolYEstado.Usuarios).UserName);
        Assert.False(porRolYEstado.EstadoFiltro);
    }

    [PostgresFact]
    public async Task Index_FiltraPorCriterioNombreApellidoOUsuario_Postgres()
    {
        await using var db = PostgresFactAttribute.CrearContexto();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var rol = new Rol { Codigo = $"T{sufijo}", Name = $"Prueba{sufijo}", NormalizedName = $"PRUEBA{sufijo}".ToUpper() };
        db.Roles.Add(rol);
        db.Usuarios.Add(new Usuario
        {
            UserName = $"zz{sufijo}",
            NormalizedUserName = $"ZZ{sufijo}".ToUpper(),
            Nombres = "Rosa",
            Apellidos = $"Quispe{sufijo}",
            Rol = rol
        });
        await db.SaveChangesAsync();

        var controller = ControladorFactory.Preparar(new UsuariosController(db, null!));
        foreach (var criterio in new[] { $"rosa quispe{sufijo}", $"QUISPE{sufijo} rosa", $"ZZ{sufijo}" })
        {
            var modelo = Assert.IsType<UsuariosIndexViewModel>(Assert.IsType<ViewResult>(
                await controller.Index(criterio, null, null)).Model);
            Assert.Single(modelo.Usuarios);
        }

        await tx.RollbackAsync();
    }

    // ---------- Create ----------

    [Fact]
    public async Task CreateGet_CargaRolesSinGerente()
    {
        using var entorno = await EntornoIdentity.CrearAsync();

        var modelo = Assert.IsType<CrearUsuarioViewModel>(Assert.IsType<ViewResult>(
            await CrearControlador(entorno).Create()).Model);

        Assert.Equal(2, modelo.Roles.Count());
        Assert.DoesNotContain(modelo.Roles, r => r.Value == entorno.RolGerente.Id.ToString());
    }

    [Fact]
    public async Task Create_DatosValidos_CreaUsuarioActivoConRolYMensaje()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var controller = CrearControlador(entorno);

        var resultado = await controller.Create(ModeloCrear(entorno.RolVendedora.Id));

        Assert.Equal(nameof(UsuariosController.Index), Assert.IsType<RedirectToActionResult>(resultado).ActionName);
        Assert.Equal("USUARIO CREADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        var usuario = await entorno.Db.Usuarios.SingleAsync();
        Assert.Equal("jperez", usuario.UserName);
        Assert.Equal("Juan", usuario.Nombres);
        Assert.Equal("Pérez", usuario.Apellidos);
        Assert.True(usuario.Estado);
        Assert.Equal(entorno.RolVendedora.Id, usuario.IdRol);
        Assert.True(await entorno.UserManager.IsInRoleAsync(usuario, "Vendedora"));
    }

    [Fact]
    public async Task Create_UsuarioDuplicado_MuestraMensajeYNoCrea()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("jperez", entorno.RolAdmin);
        var controller = CrearControlador(entorno);

        var resultado = await controller.Create(ModeloCrear(entorno.RolVendedora.Id));

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.NotEmpty(Assert.IsType<CrearUsuarioViewModel>(vista.Model).Roles);
        Assert.Contains(controller.ModelState[nameof(CrearUsuarioViewModel.UserName)]!.Errors,
            e => e.ErrorMessage == "Ya existe un usuario con ese nombre.");
        Assert.Equal(1, await entorno.Db.Usuarios.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999L)]
    public async Task Create_SinRolOInexistente_RechazaRolObligatorio(long? idRol)
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var controller = CrearControlador(entorno);

        var resultado = await controller.Create(ModeloCrear(idRol));

        Assert.IsType<ViewResult>(resultado);
        Assert.Contains(controller.ModelState[nameof(CrearUsuarioViewModel.IdRol)]!.Errors,
            e => e.ErrorMessage == "Seleccione un rol válido.");
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Create_ConRolGerente_SeRechaza()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var controller = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Create(ModeloCrear(entorno.RolGerente.Id)));
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Create_RolInactivo_SeRechaza()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        entorno.RolAdmin.Estado = false;
        await entorno.Db.SaveChangesAsync();
        var controller = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Create(ModeloCrear(entorno.RolAdmin.Id)));
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Theory]
    [InlineData("Corta1")]
    [InlineData("")]
    public void CrearUsuarioViewModel_ContrasenaMenorA8_EsInvalida(string password)
    {
        var controller = new UsuariosController(null!, null!);
        ControladorFactory.ValidarModelo(controller, ModeloCrear(1, password: password));

        Assert.True(controller.ModelState.ContainsKey(nameof(CrearUsuarioViewModel.Password)));
    }

    [Fact]
    public async Task Create_ContrasenaSinDigito_IdentityRechazaYNoCrea()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var controller = CrearControlador(entorno);

        var resultado = await controller.Create(ModeloCrear(entorno.RolVendedora.Id, password: "SoloLetras"));

        Assert.IsType<ViewResult>(resultado);
        Assert.Single(controller.ModelState[string.Empty]!.Errors);
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Create_FalloAlAsignarRol_EliminaUsuarioCreado()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        entorno.UserManager.FallarAsignarRol = true;
        var controller = CrearControlador(entorno);

        var resultado = await controller.Create(ModeloCrear(entorno.RolVendedora.Id));

        Assert.IsType<ViewResult>(resultado);
        Assert.True(entorno.UserManager.EliminarFueLlamado);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == UserManagerConFallas.ErrorSimulado.Description);
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Create_FalloDeIdentityAlCrear_DevuelveVistaConError()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        entorno.UserManager.FallarCrear = true;
        var controller = CrearControlador(entorno);

        var vista = Assert.IsType<ViewResult>(await controller.Create(ModeloCrear(entorno.RolVendedora.Id)));

        Assert.NotEmpty(Assert.IsType<CrearUsuarioViewModel>(vista.Model).Roles);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == UserManagerConFallas.ErrorSimulado.Description);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task EditGet_PrecargaDatosRolYPermisosActivos()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora, nombres: "Lucía", apellidos: "Ramos");
        var permisoActivo = new Permiso { Codigo = "P1", Nombre = "Registrar pedidos" };
        var permisoInactivo = new Permiso { Codigo = "P2", Nombre = "Oculto", Estado = false };
        entorno.Db.Set<RolPermiso>().AddRange(
            new RolPermiso { IdRol = entorno.RolVendedora.Id, Permiso = permisoActivo },
            new RolPermiso { IdRol = entorno.RolVendedora.Id, Permiso = permisoInactivo });
        await entorno.Db.SaveChangesAsync();

        var modelo = Assert.IsType<EditarUsuarioViewModel>(Assert.IsType<ViewResult>(
            await CrearControlador(entorno).Edit(usuario.Id)).Model);

        Assert.Equal("vend", modelo.UserName);
        Assert.Equal("Lucía", modelo.Nombres);
        Assert.Equal("Ramos", modelo.Apellidos);
        Assert.Equal(entorno.RolVendedora.Id, modelo.IdRol);
        Assert.True(modelo.Estado);
        Assert.Equal("Vendedor", modelo.NombreRol);
        Assert.Equal(new[] { "Registrar pedidos" }, modelo.Permisos);
    }

    [Fact]
    public async Task EditGet_UsuarioAdmin_MuestraNombreRolAdministrador()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("adm", entorno.RolAdmin);

        var modelo = Assert.IsType<EditarUsuarioViewModel>(Assert.IsType<ViewResult>(
            await CrearControlador(entorno).Edit(usuario.Id)).Model);

        Assert.Equal("Administrador", modelo.NombreRol);
    }

    [Fact]
    public async Task EditGet_RolDistinto_UsaNombreDelRol()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var rolOtro = new Rol { Name = "Repartidor", NormalizedName = "REPARTIDOR", Codigo = "OTRO" };
        entorno.Db.Roles.Add(rolOtro);
        await entorno.Db.SaveChangesAsync();
        var usuario = await entorno.CrearUsuarioAsync("otro", rolOtro);

        var modelo = Assert.IsType<EditarUsuarioViewModel>(Assert.IsType<ViewResult>(
            await CrearControlador(entorno).Edit(usuario.Id)).Model);

        Assert.Equal("Repartidor", modelo.NombreRol);
    }

    [Fact]
    public async Task EditGet_InexistenteOGerente_DevuelveNotFound()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var gerente = await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente);
        var controller = CrearControlador(entorno);

        Assert.IsType<NotFoundResult>(await controller.Edit(9999));
        Assert.IsType<NotFoundResult>(await controller.Edit(gerente.Id));
    }

    [Fact]
    public async Task EditPost_DatosValidos_ActualizaYCambiaRol()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);

        var resultado = await controller.Edit(new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            UserName = " vend.nuevo ",
            Nombres = " Ana ",
            Apellidos = " Díaz ",
            IdRol = entorno.RolAdmin.Id
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("USUARIO ACTUALIZADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        Assert.Equal("vend.nuevo", usuario.UserName);
        Assert.Equal("Ana", usuario.Nombres);
        Assert.Equal("Díaz", usuario.Apellidos);
        Assert.Equal(entorno.RolAdmin.Id, usuario.IdRol);
        Assert.Equal(new[] { "Administradora" }, await entorno.UserManager.GetRolesAsync(usuario));
    }

    [Fact]
    public async Task EditPost_UsuarioSinRolesPrevios_AsignaRolSinRemover()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = new Usuario { UserName = "sinrol", Nombres = "N", Apellidos = "A", IdRol = entorno.RolVendedora.Id };
        await entorno.UserManager.CreateAsync(usuario, "Clave1234");
        var controller = CrearControlador(entorno);

        await controller.Edit(new EditarUsuarioViewModel
        {
            Id = usuario.Id, UserName = "sinrol", Nombres = "N", Apellidos = "A", IdRol = entorno.RolVendedora.Id
        });

        Assert.Equal(new[] { "Vendedora" }, await entorno.UserManager.GetRolesAsync(usuario));
    }

    [Fact]
    public async Task EditPost_NombreDuplicadoYRolInvalido_DevuelveVistaConErrores()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("existente", entorno.RolAdmin);
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);

        var vista = Assert.IsType<ViewResult>(await controller.Edit(new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            UserName = "existente",
            Nombres = "N",
            Apellidos = "A",
            IdRol = null
        }));

        var modelo = Assert.IsType<EditarUsuarioViewModel>(vista.Model);
        Assert.Equal("Vendedor", modelo.NombreRol);
        Assert.Contains(controller.ModelState[nameof(EditarUsuarioViewModel.UserName)]!.Errors,
            e => e.ErrorMessage == "Ya existe un usuario con ese nombre.");
        Assert.True(controller.ModelState.ContainsKey(nameof(EditarUsuarioViewModel.IdRol)));
        Assert.Equal("vend", (await entorno.Db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).UserName);
    }

    [Theory]
    [InlineData("ADMIN", "Administrador")]
    [InlineData("OTRO", "Supervisor")]
    public async Task EditPost_Invalido_RecalculaNombreRolSegunCodigo(string codigo, string esperado)
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var rol = codigo == "ADMIN"
            ? entorno.RolAdmin
            : new Rol { Name = "Supervisor", NormalizedName = "SUPERVISOR", Codigo = codigo };
        if (rol.Id == 0)
        {
            entorno.Db.Roles.Add(rol);
            await entorno.Db.SaveChangesAsync();
        }
        var usuario = await entorno.CrearUsuarioAsync("u1", rol);

        var vista = Assert.IsType<ViewResult>(await CrearControlador(entorno).Edit(new EditarUsuarioViewModel
        {
            Id = usuario.Id, UserName = "u1", Nombres = "N", Apellidos = "A", IdRol = entorno.RolGerente.Id
        }));

        Assert.Equal(esperado, Assert.IsType<EditarUsuarioViewModel>(vista.Model).NombreRol);
    }

    [Fact]
    public async Task EditPost_InexistenteOGerente_DevuelveNotFound()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var gerente = await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente);
        var controller = CrearControlador(entorno);

        Assert.IsType<NotFoundResult>(await controller.Edit(new EditarUsuarioViewModel { Id = 9999 }));
        Assert.IsType<NotFoundResult>(await controller.Edit(new EditarUsuarioViewModel
        {
            Id = gerente.Id, UserName = "gerente", Nombres = "N", Apellidos = "A", IdRol = entorno.RolAdmin.Id
        }));
    }

    [Fact]
    public async Task EditPost_FalloDeIdentity_DevuelveVistaConError()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        entorno.UserManager.FallarActualizar = true;
        var controller = CrearControlador(entorno);

        var vista = Assert.IsType<ViewResult>(await controller.Edit(new EditarUsuarioViewModel
        {
            Id = usuario.Id, UserName = "vend", Nombres = "N", Apellidos = "A", IdRol = entorno.RolVendedora.Id
        }));

        Assert.NotEmpty(Assert.IsType<EditarUsuarioViewModel>(vista.Model).Roles);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == UserManagerConFallas.ErrorSimulado.Description);
    }

    // ---------- Cambiar estado ----------

    [Theory]
    [InlineData(false, "USUARIO DESACTIVADO CORRECTAMENTE")]
    [InlineData(true, "USUARIO ACTIVADO CORRECTAMENTE")]
    public async Task CambiarEstado_ActualizaEstadoYMensaje(bool estado, string mensaje)
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora, estado: !estado);
        var controller = CrearControlador(entorno);

        Assert.IsType<RedirectToActionResult>(await controller.CambiarEstado(usuario.Id, estado));

        Assert.Equal(estado, usuario.Estado);
        Assert.Equal(mensaje, controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task CambiarEstado_InexistenteOGerente_DevuelveNotFound()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var gerente = await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente);
        var controller = CrearControlador(entorno);

        Assert.IsType<NotFoundResult>(await controller.CambiarEstado(9999, false));
        Assert.IsType<NotFoundResult>(await controller.CambiarEstado(gerente.Id, false));
        Assert.True(gerente.Estado);
    }

    [Fact]
    public async Task CambiarEstado_FalloDeIdentity_MuestraError()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        entorno.UserManager.FallarActualizar = true;
        var controller = CrearControlador(entorno);

        await controller.CambiarEstado(usuario.Id, false);

        Assert.Equal(UserManagerConFallas.ErrorSimulado.Description, controller.TempData["ErrorMessage"]);
        Assert.Null(controller.TempData["SuccessMessage"]);
    }

    // ---------- Restablecer contraseña ----------

    [Fact]
    public async Task RestablecerPassword_Valido_CambiaContrasenaYMuestraMensaje()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);

        await controller.RestablecerPassword(new RestablecerPasswordViewModel
        {
            Id = usuario.Id, NuevaPassword = "NuevaClave9", ConfirmarPassword = "NuevaClave9"
        });

        Assert.Equal("CONTRASEÑA RESTABLECIDA CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        Assert.True(await entorno.UserManager.CheckPasswordAsync(usuario, "NuevaClave9"));
        Assert.False(await entorno.UserManager.CheckPasswordAsync(usuario, "Clave1234"));
    }

    [Fact]
    public async Task RestablecerPassword_SinDigito_MuestraErrorDeIdentityYConservaLaAnterior()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);

        await controller.RestablecerPassword(new RestablecerPasswordViewModel
        {
            Id = usuario.Id, NuevaPassword = "SinDigitos", ConfirmarPassword = "SinDigitos"
        });

        Assert.NotNull(controller.TempData["ErrorMessage"]);
        Assert.True(await entorno.UserManager.CheckPasswordAsync(usuario, "Clave1234"));
    }

    [Fact]
    public async Task RestablecerPassword_ConfirmacionDistinta_EsInvalidaYRedirige()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);
        var modelo = new RestablecerPasswordViewModel
        {
            Id = usuario.Id, NuevaPassword = "NuevaClave9", ConfirmarPassword = "OtraClave9"
        };
        ControladorFactory.ValidarModelo(controller, modelo);

        Assert.IsType<RedirectToActionResult>(await controller.RestablecerPassword(modelo));

        Assert.Equal("Verifique los datos del formulario.", controller.TempData["ErrorMessage"]);
        Assert.True(await entorno.UserManager.CheckPasswordAsync(usuario, "Clave1234"));
    }

    [Fact]
    public async Task RestablecerPassword_InexistenteOGerente_DevuelveNotFound()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var gerente = await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente);
        var controller = CrearControlador(entorno);

        Assert.IsType<NotFoundResult>(await controller.RestablecerPassword(new RestablecerPasswordViewModel
        {
            Id = 9999, NuevaPassword = "NuevaClave9", ConfirmarPassword = "NuevaClave9"
        }));
        Assert.IsType<NotFoundResult>(await controller.RestablecerPassword(new RestablecerPasswordViewModel
        {
            Id = gerente.Id, NuevaPassword = "NuevaClave9", ConfirmarPassword = "NuevaClave9"
        }));
    }

    // ---------- Eliminar ----------

    [Fact]
    public async Task Eliminar_Valido_BorraUsuarioYMuestraMensaje()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var controller = CrearControlador(entorno);

        Assert.IsType<RedirectToActionResult>(await controller.Eliminar(usuario.Id));

        Assert.Equal("USUARIO ELIMINADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        Assert.Empty(await entorno.Db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Eliminar_InexistenteOGerente_DevuelveNotFound()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var gerente = await entorno.CrearUsuarioAsync("gerente", entorno.RolGerente);
        var controller = CrearControlador(entorno);

        Assert.IsType<NotFoundResult>(await controller.Eliminar(9999));
        Assert.IsType<NotFoundResult>(await controller.Eliminar(gerente.Id));
        Assert.Equal(1, await entorno.Db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task Eliminar_FalloDeIdentity_MuestraError()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        entorno.UserManager.FallarEliminar = true;
        var controller = CrearControlador(entorno);

        await controller.Eliminar(usuario.Id);

        Assert.Equal(UserManagerConFallas.ErrorSimulado.Description, controller.TempData["ErrorMessage"]);
        Assert.Equal(1, await entorno.Db.Usuarios.CountAsync());
    }
}
