using System.Security.Claims;
using AguaSantaClara.Tests.Soporte;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Tests;

public class AccountControllerTests
{
    private static (AccountController Controller, AutenticacionFalsa Auth) CrearControlador(EntornoIdentity entorno)
    {
        var auth = new AutenticacionFalsa();
        var controller = ControladorFactory.Preparar(new AccountController(entorno.Db, entorno.Hasher), auth: auth);
        return (controller, auth);
    }

    [Fact]
    public async Task LoginGet_GuardaReturnUrlEnViewBag()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var (controller, _) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(controller.Login("/Pedidos"));
        Assert.Equal("/Pedidos", controller.ViewBag.ReturnUrl);
    }

    [Fact]
    public async Task Login_CredencialesValidas_FirmaClaimsDeRolYRedirigeAHome()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora, nombres: "Lucía", apellidos: "Ramos");
        var (controller, auth) = CrearControlador(entorno);

        var resultado = await controller.Login(new LoginViewModel { Username = "vend", Password = "Clave1234" });

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Index", redireccion.ActionName);
        Assert.Equal("Home", redireccion.ControllerName);
        Assert.NotNull(auth.UsuarioFirmado);
        Assert.Equal("Vendedora", auth.UsuarioFirmado!.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(usuario.Id.ToString(), auth.UsuarioFirmado.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Lucía Ramos", auth.UsuarioFirmado.FindFirstValue("NombreCompleto"));
    }

    [Fact]
    public async Task Login_ConReturnUrlLocal_RedirigeAEsaRuta()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var (controller, _) = CrearControlador(entorno);

        var resultado = await controller.Login(new LoginViewModel
        {
            Username = "vend", Password = "Clave1234", ReturnUrl = "/Pedidos"
        });

        Assert.Equal("/Pedidos", Assert.IsType<RedirectResult>(resultado).Url);
    }

    [Fact]
    public async Task Login_ConReturnUrlExterna_IgnoraYRedirigeAHome()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var (controller, _) = CrearControlador(entorno);

        var resultado = await controller.Login(new LoginViewModel
        {
            Username = "vend", Password = "Clave1234", ReturnUrl = "https://malicioso.example"
        });

        Assert.Equal("Home", Assert.IsType<RedirectToActionResult>(resultado).ControllerName);
    }

    [Fact]
    public async Task Login_ModeloInvalido_DevuelveVistaSinConsultar()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var (controller, auth) = CrearControlador(entorno);
        controller.ModelState.AddModelError("Username", "El nombre de usuario es obligatorio.");

        var vista = Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel()));

        Assert.IsType<LoginViewModel>(vista.Model);
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_MuestraMensajeGenerico()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var (controller, auth) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel { Username = "nadie", Password = "Clave1234" }));

        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == "Usuario o contraseña incorrectos.");
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Login_UsuarioEliminadoLogicamente_NoInicia()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("borrado", entorno.RolVendedora);
        usuario.EstadoRegistro = false;
        await entorno.Db.SaveChangesAsync();
        var (controller, auth) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel { Username = "borrado", Password = "Clave1234" }));
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Login_UsuarioInactivo_NoIniciaSesion()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("inactivo", entorno.RolVendedora, estado: false);
        var (controller, auth) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel { Username = "inactivo", Password = "Clave1234" }));

        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage == "El usuario se encuentra inactivo. Contacte al administrador.");
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Login_ContrasenaIncorrecta_MuestraMensajeGenerico()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var (controller, auth) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel { Username = "vend", Password = "Incorrecta1" }));

        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == "Usuario o contraseña incorrectos.");
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Login_UsuarioSinPasswordHash_NoInicia()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("sinhash", entorno.RolVendedora);
        usuario.PasswordHash = null;
        await entorno.Db.SaveChangesAsync();
        var (controller, auth) = CrearControlador(entorno);

        Assert.IsType<ViewResult>(await controller.Login(new LoginViewModel { Username = "sinhash", Password = "Clave1234" }));
        Assert.Null(auth.UsuarioFirmado);
    }

    [Fact]
    public async Task Logout_CierraSesionYRedirigeALogin()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var (controller, auth) = CrearControlador(entorno);

        var redireccion = Assert.IsType<RedirectToActionResult>(await controller.Logout());

        Assert.True(auth.SesionCerrada);
        Assert.Equal("Login", redireccion.ActionName);
        Assert.Equal("Account", redireccion.ControllerName);
    }
}
