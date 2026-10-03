using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class PedidoServiceTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private class Escenario
    {
        public AppDbContext Db { get; init; } = null!;
        public PedidoService Servicio { get; init; } = null!;
        public Local Local { get; init; } = null!;
        public Producto Galon { get; init; } = null!;
        public Producto Hielo { get; init; } = null!;
        public Cliente Ana { get; init; } = null!;
        public Cliente Beto { get; init; } = null!;
        public Repartidor Repartidor { get; init; } = null!;
    }

    private static async Task<Escenario> CrearEscenarioAsync(int stockGalon = 10, int stockHielo = 3)
    {
        var db = CrearContexto();
        var local = new Local { Nombre = "Santa Rosa" };
        var galon = new Producto { Nombre = "Galones 11L", PrecioVenta = 12.50m, Costo = 7m };
        var hielo = new Producto { Nombre = "Bolsas de Hielo", PrecioVenta = 5m, Costo = 2m };
        var ana = new Cliente
        {
            Nombre = "Ana Torres", Telefono = "999111222", Dni = "12345678",
            Direcciones =
            {
                new DireccionCliente { Direccion = "Av. Lima 123", Ciudad = "Lima", Principal = true, UrlUbicacion = "https://maps.app.goo.gl/ana1" },
                new DireccionCliente { Direccion = "Jr. Cusco 45", UrlUbicacion = "https://maps.app.goo.gl/ana2" }
            }
        };
        var beto = new Cliente
        {
            Nombre = "Beto Ruiz", Telefono = "999333444",
            Direcciones = { new DireccionCliente { Direccion = "Calle Sol 9", Principal = true } }
        };
        var repartidor = new Repartidor { Nombre = "Carlos Ramos", Celular = "51987654321" };

        db.AddRange(local, galon, hielo, ana, beto, repartidor);
        await db.SaveChangesAsync();

        db.ProductosLocal.AddRange(
            new ProductoLocal { IdLocal = local.Id, IdProducto = galon.Id, Stock = stockGalon },
            new ProductoLocal { IdLocal = local.Id, IdProducto = hielo.Id, Stock = stockHielo });
        await db.SaveChangesAsync();

        return new Escenario
        {
            Db = db, Servicio = new PedidoService(db), Local = local, Galon = galon,
            Hielo = hielo, Ana = ana, Beto = beto, Repartidor = repartidor
        };
    }

    private static PedidoClienteViewModel Linea(Cliente cliente, int indiceDireccion, params (Producto producto, int cantidad)[] items) => new()
    {
        IdCliente = cliente.Id,
        IdDireccion = cliente.Direcciones.OrderBy(d => d.Id).ElementAt(indiceDireccion).Id,
        Detalles = items.Select(i => new DetallePedidoViewModel { IdProducto = i.producto.Id, Cantidad = i.cantidad }).ToList()
    };

    private static CrearPedidoViewModel Modelo(Escenario e, params PedidoClienteViewModel[] lineas) => new()
    {
        Clientes = lineas.ToList()
    };

    [Fact]
    public async Task Crear_CalculaTotalComoCantidadPorPrecioDeCatalogo()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(e, Linea(e.Ana, 0, (e.Galon, 2), (e.Hielo, 1)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.True(resultado.Ok);
        Assert.Equal(30m, resultado.Total);
        var pedido = await e.Db.Pedidos.Include(p => p.Clientes).ThenInclude(c => c.Detalles).SingleAsync();
        Assert.Equal(30m, pedido.Total);
        Assert.Equal(30m, pedido.Clientes.Single().Subtotal);
        var detalleGalon = pedido.Clientes.Single().Detalles.Single(d => d.IdProducto == e.Galon.Id);
        Assert.Equal(12.50m, detalleGalon.PrecioUnitario);
        Assert.Equal(25m, detalleGalon.Subtotal);
    }

    [Fact]
    public async Task Crear_GuardarRegistraPedidoPendienteSinUrl()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: false);

        Assert.True(resultado.Ok);
        Assert.Null(resultado.WhatsappUrl);
        Assert.Equal(EstadosPedido.Pendiente, (await e.Db.Pedidos.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Actualizar_ReemplazaDatosYDetallesSinCrearOtroPedido()
    {
        var e = await CrearEscenarioAsync();
        var creado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: false);

        var actualizado = await e.Servicio.ActualizarAsync(creado.IdPedido, Modelo(e, Linea(e.Beto, 0, (e.Hielo, 3))));

        Assert.True(actualizado.Ok);
        Assert.Equal(1, await e.Db.Pedidos.CountAsync());
        var pedido = await e.Db.Pedidos
            .Include(p => p.Clientes).ThenInclude(c => c.Detalles)
            .SingleAsync();
        Assert.Equal(creado.IdPedido, pedido.Id);
        Assert.Equal(15m, pedido.Total);
        Assert.Equal(e.Beto.Id, pedido.Clientes.Single().IdCliente);
        Assert.Equal(e.Hielo.Id, pedido.Clientes.Single().Detalles.Single().IdProducto);
        Assert.Equal(3, pedido.Clientes.Single().Detalles.Single().Cantidad);
    }

    [Fact]
    public async Task Eliminar_ExcluyePedidoDelListadoSinBorrarElRegistro()
    {
        var e = await CrearEscenarioAsync();
        var creado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: false);

        var eliminado = await e.Servicio.EliminarAsync(creado.IdPedido);

        Assert.True(eliminado);
        Assert.Equal(1, await e.Db.Pedidos.CountAsync());
        Assert.False((await e.Db.Pedidos.SingleAsync()).EstadoRegistro);
        Assert.Equal(0, await e.Db.Pedidos.CountAsync(p => p.EstadoRegistro));
    }

    [Fact]
    public async Task Crear_CantidadIgualAlStockSePermite()
    {
        var e = await CrearEscenarioAsync(stockGalon: 10);

        var resultado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 10))), enviar: false);

        Assert.True(resultado.Ok);
        Assert.Equal(1, await e.Db.Pedidos.CountAsync());
    }

    [Fact]
    public async Task Crear_CantidadMayorAlStockSePermiteComoBackorder()
    {
        var e = await CrearEscenarioAsync(stockGalon: 10);

        var resultado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 11)), Linea(e.Beto, 0, (e.Galon, 6))), enviar: false);

        Assert.True(resultado.Ok);
        Assert.Equal(1, await e.Db.Pedidos.CountAsync());
        Assert.Null((await e.Db.Pedidos.SingleAsync()).IdLocal);
    }

    [Fact]
    public async Task Crear_NoDescuentaElStockDelLocal()
    {
        var e = await CrearEscenarioAsync(stockGalon: 10);

        await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 4))), enviar: false);

        var stock = await e.Db.ProductosLocal.Where(p => p.IdProducto == e.Galon.Id).Select(p => p.Stock).SingleAsync();
        Assert.Equal(10, stock);
    }

    [Fact]
    public async Task Crear_SinDatosObligatoriosDevuelveUnErrorPorCampo()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(new CrearPedidoViewModel(), enviar: false);

        Assert.False(resultado.Ok);
        var campos = resultado.Errores.Select(x => x.Campo).ToList();
        Assert.Contains("clientes", campos);
        Assert.Equal(0, await e.Db.Pedidos.CountAsync());
    }

    [Fact]
    public async Task Crear_ClienteSinProductosOCantidadCeroSeRechaza()
    {
        var e = await CrearEscenarioAsync();
        var sinProductos = Linea(e.Ana, 0);
        var cantidadCero = Linea(e.Beto, 0, (e.Galon, 0));

        var resultado = await e.Servicio.CrearAsync(Modelo(e, sinProductos, cantidadCero), enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles");
        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[1].detalles[0].cantidad");
    }

    [Fact]
    public async Task Crear_ProductoInactivoSeRechaza()
    {
        var e = await CrearEscenarioAsync();
        var otro = new Producto { Nombre = "Papel Higiénico", PrecioVenta = 25m, Costo = 18m, Estado = false };
        e.Db.Productos.Add(otro);
        await e.Db.SaveChangesAsync();

        var resultado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (otro, 1))), enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].idProducto");
    }

    [Fact]
    public async Task Crear_DireccionQueNoPerteneceAlClienteSeRechaza()
    {
        var e = await CrearEscenarioAsync();
        var linea = Linea(e.Ana, 0, (e.Galon, 1));
        linea.IdDireccion = e.Beto.Direcciones.Single().Id;

        var resultado = await e.Servicio.CrearAsync(Modelo(e, linea), enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].idDireccion");
    }

    [Fact]
    public async Task Crear_MismoClienteConDosDireccionesGeneraDosLineas()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(e, Linea(e.Ana, 0, (e.Galon, 1)), Linea(e.Ana, 1, (e.Hielo, 2)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.True(resultado.Ok);
        Assert.Equal(2, await e.Db.PedidosCliente.CountAsync());
        Assert.Equal(22.50m, resultado.Total);
    }

    [Fact]
    public async Task Crear_MismaDireccionDelMismoClienteDosVecesSeRechaza()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(e, Linea(e.Ana, 0, (e.Galon, 1)), Linea(e.Ana, 0, (e.Hielo, 1)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[1].idDireccion");
    }

    [Fact]
    public async Task Enviar_SinRepartidorSeRechaza()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: true);

        Assert.Contains(resultado.Errores, x => x.Campo == "idRepartidor");
        Assert.Equal(0, await e.Db.Pedidos.CountAsync());
    }

    [Fact]
    public async Task Enviar_RepartidorConCelularInvalidoSeRechaza()
    {
        var e = await CrearEscenarioAsync();
        e.Repartidor.Celular = "+51987654321";
        await e.Db.SaveChangesAsync();
        var modelo = Modelo(e, Linea(e.Ana, 0, (e.Galon, 1)));
        modelo.IdRepartidor = e.Repartidor.Id;

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: true);

        Assert.Contains(resultado.Errores, x => x.Campo == "idRepartidor");
    }

    [Fact]
    public async Task Enviar_MarcaElPedidoComoEnviadoYGeneraLaUrlDeWhatsapp()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(e, Linea(e.Ana, 0, (e.Galon, 2)));
        modelo.IdRepartidor = e.Repartidor.Id;

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: true);

        Assert.True(resultado.Ok);
        var pedido = await e.Db.Pedidos.SingleAsync();
        Assert.Equal(EstadosPedido.Enviado, pedido.Estado);
        Assert.Equal(e.Repartidor.Id, pedido.IdRepartidor);
        Assert.StartsWith("https://wa.me/51987654321?text=", resultado.WhatsappUrl);
        Assert.Contains(Uri.EscapeDataString("Ubicación: https://maps.app.goo.gl/ana1"), resultado.WhatsappUrl);
    }

    [Theory]
    [InlineData("51987654321", true)]
    [InlineData("+51987654321", false)]
    [InlineData("987654321", false)]
    [InlineData("51887654321", false)]
    [InlineData("5198765432", false)]
    [InlineData("519876543210", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CelularValido_AceptaSoloFormatoInternacionalPeruanoSinMas(string? celular, bool esperado)
    {
        Assert.Equal(esperado, PedidoService.CelularValido(celular));
    }

    [Fact]
    public async Task BuscarCliente_EncuentraPorTelefonoOPorDni()
    {
        var e = await CrearEscenarioAsync();

        var porTelefono = await e.Servicio.BuscarClienteAsync("999111222");
        var porDni = await e.Servicio.BuscarClienteAsync("12345678");
        var inexistente = await e.Servicio.BuscarClienteAsync("00000000");

        Assert.Equal(e.Ana.Id, porTelefono?.Id);
        Assert.Equal(e.Ana.Id, porDni?.Id);
        Assert.Equal(2, porDni!.Direcciones.Count);
        Assert.Null(inexistente);
    }

    [Fact]
    public async Task ProductosDisponibles_ListaProductosActivosSinDependerDelLocal()
    {
        var e = await CrearEscenarioAsync();
        e.Db.Productos.Add(new Producto { Nombre = "Inactivo", PrecioVenta = 1m, Estado = false });
        await e.Db.SaveChangesAsync();

        var productos = await e.Servicio.ProductosDisponiblesAsync();

        Assert.Equal(2, productos.Count);
        Assert.DoesNotContain(productos, p => p.Nombre == "Inactivo");
    }

    [Fact]
    public async Task EnviarVarios_AsignaRepartidorMarcaEnviadosYArmaUrlConTodos()
    {
        var e = await CrearEscenarioAsync();
        await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: false);
        await e.Servicio.CrearAsync(Modelo(e, Linea(e.Beto, 0, (e.Galon, 2))), enviar: false);
        var ids = await e.Db.Pedidos.Select(p => p.Id).ToListAsync();

        var resultado = await e.Servicio.EnviarVariosAsync(new EnviarPedidosViewModel { IdsPedidos = ids, IdRepartidor = e.Repartidor.Id });

        Assert.True(resultado.Ok);
        Assert.All(await e.Db.Pedidos.ToListAsync(), p =>
        {
            Assert.Equal(EstadosPedido.Enviado, p.Estado);
            Assert.Equal(e.Repartidor.Id, p.IdRepartidor);
        });
        Assert.Contains(Uri.EscapeDataString("Pedidos asignados: 2"), resultado.WhatsappUrl);
    }

    [Fact]
    public async Task EnviarVarios_SinPedidosORepartidorDevuelveErrores()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.EnviarVariosAsync(new EnviarPedidosViewModel());

        Assert.False(resultado.Ok);
        var campos = resultado.Errores.Select(x => x.Campo).ToList();
        Assert.Contains("pedidos", campos);
        Assert.Contains("idRepartidor", campos);
    }

    [Fact]
    public async Task EnviarVarios_RechazaPedidosQueNoEstanPendientes()
    {
        var e = await CrearEscenarioAsync();
        await e.Servicio.CrearAsync(Modelo(e, Linea(e.Ana, 0, (e.Galon, 1))), enviar: false);
        var id = (await e.Db.Pedidos.SingleAsync()).Id;
        var modelo = new EnviarPedidosViewModel { IdsPedidos = { id }, IdRepartidor = e.Repartidor.Id };
        await e.Servicio.EnviarVariosAsync(modelo);

        var resultado = await e.Servicio.EnviarVariosAsync(modelo);

        Assert.False(resultado.Ok);
    }
}
