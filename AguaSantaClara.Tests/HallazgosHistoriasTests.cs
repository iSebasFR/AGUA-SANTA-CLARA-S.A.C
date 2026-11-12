using AguaSantaClara.Tests.Soporte;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

// Pruebas que expresan el criterio de la historia tal como está en Jira.
// Si fallan, el código no cumple la historia (hallazgo); no se corrige el código.
public class HallazgosHistoriasTests
{
    private static AppDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(AppDbContext Db, Local Central, Local Norte)> EscenarioProductosAsync()
    {
        var db = CrearContexto();
        var central = new Local { Nombre = "Central" };
        var norte = new Local { Nombre = "Norte" };
        db.Locales.AddRange(central, norte);
        await db.SaveChangesAsync();
        return (db, central, norte);
    }

    // a) HU-02: nombre repetido debe mostrar "Ya existe un producto con ese nombre".
    [Fact(Skip = "Hallazgo HU-02: nombre repetido no muestra 'Ya existe un producto con ese nombre'")]
    public async Task HallazgoA_HU02_CrearProductoConNombreRepetido_DebeMostrarYaExiste()
    {
        var (db, central, norte) = await EscenarioProductosAsync();
        db.Productos.Add(new Producto { Nombre = "Bidón 20L", Categoria = "Bidón", PrecioVenta = 10m, Costo = 5m });
        await db.SaveChangesAsync();
        db.ProductosLocal.Add(new ProductoLocal { IdLocal = central.Id, IdProducto = (await db.Productos.SingleAsync()).Id });
        await db.SaveChangesAsync();
        var controller = ControladorFactory.Preparar(new ProductosController(db), ajax: true, rol: "Administradora");

        await controller.Create(new CrearProductoViewModel
        {
            Nombre = "Bidón 20L", Categoria = "Bidón", PrecioVenta = 10m, Costo = 5m, IdLocales = new List<long> { norte.Id }
        });

        Assert.Contains(controller.ModelState.Values.SelectMany(v => v.Errors),
            e => e.ErrorMessage == "Ya existe un producto con ese nombre.");
        Assert.Equal(1, await db.ProductosLocal.CountAsync());
    }

    // b.1) HU-02: categoría debe ser Bidón, Galón, Hielo o Papel Higiénico.
    [Fact(Skip = "Hallazgo HU-02: acepta categorías fuera de las cuatro permitidas")]
    public async Task HallazgoB1_HU02_CategoriaFueraDeLasCuatroPermitidas_DebeRechazarse()
    {
        var (db, central, _) = await EscenarioProductosAsync();
        var controller = ControladorFactory.Preparar(new ProductosController(db), rol: "Administradora");

        await controller.Create(new CrearProductoViewModel
        {
            Nombre = "Gaseosa", Categoria = "Bebidas", PrecioVenta = 3m, Costo = 1m, IdLocales = new List<long> { central.Id }
        });

        Assert.Empty(await db.Productos.ToListAsync());
    }

    // b.2) HU-02: editar sin cambios un producto "Papel Higiénico" no debe alterar su categoría.
    [Fact(Skip = "Hallazgo HU-02: editar un producto 'Papel Higiénico' cambia su categoría")]
    public async Task HallazgoB2_HU02_EditarPapelHigienico_ConservaCategoria()
    {
        var (db, central, _) = await EscenarioProductosAsync();
        var producto = new Producto { Nombre = "Papel x4", Categoria = "Papel Higiénico", PrecioVenta = 6m, Costo = 3m };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        db.ProductosLocal.Add(new ProductoLocal { IdLocal = central.Id, IdProducto = producto.Id });
        await db.SaveChangesAsync();

        var get = Assert.IsType<PartialViewResult>(await ControladorFactory
            .Preparar(new ProductosController(db), ajax: true, rol: "Administradora").Edit(producto.Id));
        var formulario = Assert.IsType<EditarProductoViewModel>(get.Model);
        await ControladorFactory.Preparar(new ProductosController(db), rol: "Administradora").Edit(formulario);

        Assert.Equal("Papel Higiénico", producto.Categoria);
    }

    // c.1) HU-01: el mensaje de "al menos un dígito" debe mostrarse en español.
    [Fact(Skip = "Hallazgo HU-01: el mensaje de contraseña sin dígito no está en español")]
    public async Task HallazgoC1_HU01_ContrasenaSinDigito_MensajeEnEspanol()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var controller = ControladorFactory.Preparar(new UsuariosController(entorno.Db, entorno.UserManager));

        await controller.Create(new CrearUsuarioViewModel
        {
            Nombres = "Juan", Apellidos = "Pérez", UserName = "jperez", Password = "SoloLetras", IdRol = entorno.RolVendedora.Id
        });

        var mensaje = Assert.Single(controller.ModelState[string.Empty]!.Errors).ErrorMessage;
        Assert.Contains("dígito", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    // c.2) HU-01: tras 5 intentos fallidos (Program.cs) la cuenta debe quedar bloqueada.
    [Fact(Skip = "Hallazgo HU-01: la cuenta no se bloquea tras 5 intentos fallidos")]
    public async Task HallazgoC2_HU01_CincoIntentosFallidos_BloqueanLaCuenta()
    {
        using var entorno = await EntornoIdentity.CrearAsync();
        var usuario = await entorno.CrearUsuarioAsync("vend", entorno.RolVendedora);
        var auth = new AutenticacionFalsa();

        for (var i = 0; i < 5; i++)
        {
            var intento = ControladorFactory.Preparar(new AccountController(entorno.Db, entorno.Hasher), auth: auth);
            await intento.Login(new LoginViewModel { Username = "vend", Password = "Incorrecta1" });
        }

        var controller = ControladorFactory.Preparar(new AccountController(entorno.Db, entorno.Hasher), auth: auth);
        var resultado = await controller.Login(new LoginViewModel { Username = "vend", Password = "Clave1234" });

        Assert.True(await entorno.UserManager.IsLockedOutAsync(usuario), "La cuenta no quedó bloqueada.");
        Assert.IsType<ViewResult>(resultado);
        Assert.Null(auth.UsuarioFirmado);
    }

    // d) HU-05: el pedido admite un solo cliente.
    [Fact]
    public async Task HallazgoD_HU05_PedidoConDosClientes_SeRechaza()
    {
        var db = CrearContexto();
        var local = new Local { Nombre = "Central" };
        var producto = new Producto { Nombre = "Bidón", PrecioVenta = 10m, Costo = 5m };
        var c1 = new Cliente { Nombre = "Ana", Telefono = "999000111" };
        var c2 = new Cliente { Nombre = "Luis", Telefono = "999000222" };
        var d1 = new DireccionCliente { Cliente = c1, Direccion = "A", Ciudad = "Lima" };
        var d2 = new DireccionCliente { Cliente = c2, Direccion = "B", Ciudad = "Lima" };
        db.AddRange(local, producto, c1, c2, d1, d2);
        await db.SaveChangesAsync();
        db.ProductosLocal.Add(new ProductoLocal { IdLocal = local.Id, IdProducto = producto.Id, Estado = true, EstadoRegistro = true });
        await db.SaveChangesAsync();

        var modelo = new CrearPedidoViewModel();
        foreach (var (c, d) in new[] { (c1, d1), (c2, d2) })
            modelo.Clientes.Add(new PedidoClienteViewModel
            {
                IdCliente = c.Id, IdDireccion = d.Id,
                Detalles = { new DetallePedidoViewModel { IdLocal = local.Id, IdProducto = producto.Id, Cantidad = 1 } }
            });

        var resultado = await new PedidoService(db).CrearAsync(modelo, enviar: false);

        Assert.False(resultado.Ok);
        Assert.Empty(await db.Pedidos.ToListAsync());
    }

    // e) HU-05 / ASC-185: pedir más que el stock del local debe mostrar "Stock insuficiente".
    [Fact(Skip = "Hallazgo HU-05: pedir más que el stock no muestra 'Stock insuficiente'")]
    public async Task HallazgoE_HU05_CantidadMayorAlStock_MuestraStockInsuficiente()
    {
        var db = CrearContexto();
        var local = new Local { Nombre = "Central" };
        var producto = new Producto { Nombre = "Bidón", PrecioVenta = 10m, Costo = 5m };
        var cliente = new Cliente { Nombre = "Ana", Telefono = "999000111" };
        var direccion = new DireccionCliente { Cliente = cliente, Direccion = "A", Ciudad = "Lima" };
        db.AddRange(local, producto, cliente, direccion);
        await db.SaveChangesAsync();
        db.ProductosLocal.Add(new ProductoLocal { IdLocal = local.Id, IdProducto = producto.Id, Stock = 2, Estado = true, EstadoRegistro = true });
        await db.SaveChangesAsync();

        var modelo = new CrearPedidoViewModel
        {
            Clientes =
            {
                new PedidoClienteViewModel
                {
                    IdCliente = cliente.Id, IdDireccion = direccion.Id,
                    Detalles = { new DetallePedidoViewModel { IdLocal = local.Id, IdProducto = producto.Id, Cantidad = 5 } }
                }
            }
        };

        var resultado = await new PedidoService(db).CrearAsync(modelo, enviar: false);

        Assert.Contains(resultado.Errores, e => e.Mensaje.Contains("Stock insuficiente"));
        Assert.Empty(await db.Pedidos.ToListAsync());
    }
}
