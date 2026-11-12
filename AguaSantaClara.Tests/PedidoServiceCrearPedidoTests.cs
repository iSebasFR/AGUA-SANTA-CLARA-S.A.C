using AguaSantaClara.Tests.Soporte;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

[Collection(PostgresCollection.Nombre)]
public class PedidoServiceCrearPedidoTests
{
    private sealed record Escenario(
        AppDbContext Db, PedidoService Servicio, Local Local, Local OtroLocal, Producto Producto,
        Producto ProductoInactivo, Producto ProductoSoloOtroLocal, Cliente Cliente, DireccionCliente Direccion,
        DireccionCliente Direccion2, Cliente Cliente2, DireccionCliente DireccionCliente2, Repartidor Repartidor);

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var local = new Local { Nombre = "Central" };
        var otroLocal = new Local { Nombre = "Norte" };
        var producto = new Producto { Nombre = "Bidón 20L", PrecioVenta = 10m, Costo = 5m };
        var inactivo = new Producto { Nombre = "Galón viejo", PrecioVenta = 8m, Costo = 4m, Estado = false };
        var soloOtro = new Producto { Nombre = "Hielo 3kg", PrecioVenta = 6m, Costo = 3m };
        var cliente = new Cliente { Nombre = "Ana Torres", Telefono = "999000111", Dni = "12345678" };
        var direccion = new DireccionCliente { Cliente = cliente, Direccion = "Av. Lima 123", Ciudad = "Lima", Principal = true };
        var direccion2 = new DireccionCliente { Cliente = cliente, Direccion = "Jr. Cusco 456", Ciudad = "Lima" };
        var cliente2 = new Cliente { Nombre = "Luis Pérez", Telefono = "999222333", Dni = "87654321" };
        var direccionCliente2 = new DireccionCliente { Cliente = cliente2, Direccion = "Calle Sol 1", Ciudad = "Lima" };
        var repartidor = new Repartidor { Nombre = "Carlos", Celular = "51987654321", Estado = true, EstadoRegistro = true };

        db.AddRange(local, otroLocal, producto, inactivo, soloOtro, cliente, cliente2, repartidor);
        db.AddRange(direccion, direccion2, direccionCliente2);
        await db.SaveChangesAsync();

        db.ProductosLocal.AddRange(
            new ProductoLocal { IdLocal = local.Id, IdProducto = producto.Id, Stock = 10, Estado = true, EstadoRegistro = true },
            new ProductoLocal { IdLocal = local.Id, IdProducto = inactivo.Id, Stock = 10, Estado = true, EstadoRegistro = true },
            new ProductoLocal { IdLocal = otroLocal.Id, IdProducto = soloOtro.Id, Stock = 10, Estado = true, EstadoRegistro = true });
        await db.SaveChangesAsync();

        return new Escenario(db, new PedidoService(db), local, otroLocal, producto, inactivo, soloOtro,
            cliente, direccion, direccion2, cliente2, direccionCliente2, repartidor);
    }

    private static PedidoClienteViewModel Linea(long idCliente, long idDireccion, params DetallePedidoViewModel[] detalles)
    {
        var linea = new PedidoClienteViewModel { IdCliente = idCliente, IdDireccion = idDireccion };
        linea.Detalles.AddRange(detalles);
        return linea;
    }

    private static DetallePedidoViewModel Detalle(long idLocal, long idProducto, int cantidad = 1, decimal descuento = 0m) =>
        new() { IdLocal = idLocal, IdProducto = idProducto, Cantidad = cantidad, DescuentoMonto = descuento };

    private static CrearPedidoViewModel Modelo(long? idRepartidor, params PedidoClienteViewModel[] lineas)
    {
        var modelo = new CrearPedidoViewModel { IdRepartidor = idRepartidor };
        modelo.Clientes.AddRange(lineas);
        return modelo;
    }

    private CrearPedidoViewModel ModeloValido(Escenario e, long? idRepartidor = null, int cantidad = 2, decimal descuento = 0m) =>
        Modelo(idRepartidor, Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.Producto.Id, cantidad, descuento)));

    // ---------- Reglas de cliente / dirección ----------

    [Fact]
    public async Task CrearAsync_SinClientes_DevuelveError()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(new CrearPedidoViewModel(), enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes" && x.Mensaje == "Agrega al menos un cliente.");
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_DosClientesDistintos_RechazaUnSoloClientePorPedido()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(null,
            Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.Producto.Id)),
            Linea(e.Cliente2.Id, e.DireccionCliente2.Id, Detalle(e.Local.Id, e.Producto.Id)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes" && x.Mensaje == "Solo se permite un cliente por pedido.");
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_MismoClienteConVariasDirecciones_GuardaYSumaTotal()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(null,
            Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.Producto.Id, 2, 1m)),
            Linea(e.Cliente.Id, e.Direccion2.Id, Detalle(e.Local.Id, e.Producto.Id, 3), Detalle(e.OtroLocal.Id, e.ProductoSoloOtroLocal.Id, 1)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.True(resultado.Ok, string.Join("; ", resultado.Errores.Select(x => x.Mensaje)));
        Assert.Equal(19m + 30m + 6m, resultado.Total);
        var pedido = await e.Db.Pedidos.Include(p => p.Clientes).ThenInclude(c => c.Detalles).SingleAsync();
        Assert.Equal(EstadosPedido.Pendiente, pedido.Estado);
        Assert.Null(pedido.IdRepartidor);
        Assert.Equal(2, pedido.Clientes.Count);
        var detalle = pedido.Clientes.Single(c => c.IdDireccion == e.Direccion.Id).Detalles.Single();
        Assert.Equal(10m, detalle.PrecioUnitario);
        Assert.Equal(19m, detalle.Subtotal);
    }

    [Fact]
    public async Task CrearAsync_MismaDireccionRepetida_DevuelveError()
    {
        var e = await CrearEscenarioAsync();
        var modelo = Modelo(null,
            Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.Producto.Id)),
            Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.Producto.Id)));

        var resultado = await e.Servicio.CrearAsync(modelo, enviar: false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[1].idDireccion"
            && x.Mensaje == "Esa dirección ya está agregada para este cliente.");
    }

    [Fact]
    public async Task CrearAsync_ClienteInexistenteOInactivo_DevuelveError()
    {
        var e = await CrearEscenarioAsync();
        e.Cliente2.Estado = false;
        await e.Db.SaveChangesAsync();

        var inexistente = await e.Servicio.CrearAsync(Modelo(null, Linea(9999, 1, Detalle(e.Local.Id, e.Producto.Id))), false);
        var inactivo = await e.Servicio.CrearAsync(
            Modelo(null, Linea(e.Cliente2.Id, e.DireccionCliente2.Id, Detalle(e.Local.Id, e.Producto.Id))), false);

        Assert.Contains(inexistente.Errores, x => x.Campo == "clientes[0].idCliente");
        Assert.Contains(inactivo.Errores, x => x.Campo == "clientes[0].idCliente");
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_DireccionDeOtroClienteOEliminada_DevuelveError()
    {
        var e = await CrearEscenarioAsync();
        e.Direccion2.EstadoRegistro = false;
        await e.Db.SaveChangesAsync();

        var ajena = await e.Servicio.CrearAsync(
            Modelo(null, Linea(e.Cliente.Id, e.DireccionCliente2.Id, Detalle(e.Local.Id, e.Producto.Id))), false);
        var eliminada = await e.Servicio.CrearAsync(
            Modelo(null, Linea(e.Cliente.Id, e.Direccion2.Id, Detalle(e.Local.Id, e.Producto.Id))), false);

        Assert.Contains(ajena.Errores, x => x.Campo == "clientes[0].idDireccion");
        Assert.Contains(eliminada.Errores, x => x.Campo == "clientes[0].idDireccion");
    }

    [Fact]
    public async Task CrearAsync_SinProductos_DevuelveError()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(Modelo(null, Linea(e.Cliente.Id, e.Direccion.Id)), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles" && x.Mensaje == "Agrega al menos un producto.");
    }

    // ---------- Reglas de producto / cantidad / descuento ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CrearAsync_CantidadMenorOIgualACero_DevuelveError(int cantidad)
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, cantidad: cantidad), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].cantidad"
            && x.Mensaje == "La cantidad debe ser mayor a cero.");
    }

    [Fact]
    public async Task CrearAsync_DescuentoNegativo_DevuelveError()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, descuento: -1m), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].descuentoMonto"
            && x.Mensaje == "El descuento no puede ser negativo.");
    }

    [Fact]
    public async Task CrearAsync_DescuentoMayorAlSubtotal_IndicaMontoMaximo()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, cantidad: 2, descuento: 20.01m), false);

        Assert.Contains(resultado.Errores, x => x.Mensaje == "El descuento no puede superar el monto del producto (20.00).");
    }

    [Fact]
    public async Task CrearAsync_DescuentoIgualAlSubtotal_TotalCero()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, cantidad: 2, descuento: 20m), false);

        Assert.True(resultado.Ok);
        Assert.Equal(0m, resultado.Total);
    }

    [Fact]
    public async Task CrearAsync_ProductoInactivo_NoDisponible()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(
            Modelo(null, Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.ProductoInactivo.Id))), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].idProducto"
            && x.Mensaje == "El producto no está disponible en el local seleccionado.");
    }

    [Fact]
    public async Task CrearAsync_ProductoDeOtroLocal_NoDisponible()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(
            Modelo(null, Linea(e.Cliente.Id, e.Direccion.Id, Detalle(e.Local.Id, e.ProductoSoloOtroLocal.Id))), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].idProducto");
    }

    [Fact]
    public async Task CrearAsync_RelacionProductoLocalInactiva_NoDisponible()
    {
        var e = await CrearEscenarioAsync();
        (await e.Db.ProductosLocal.SingleAsync(pl => pl.IdProducto == e.Producto.Id)).Estado = false;
        await e.Db.SaveChangesAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e), false);

        Assert.Contains(resultado.Errores, x => x.Campo == "clientes[0].detalles[0].idProducto");
    }

    // ---------- Guardar y enviar ----------

    [Fact]
    public async Task CrearAsync_EnviarSinRepartidor_DevuelveErrorYNoGuarda()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: null), enviar: true);

        Assert.Contains(resultado.Errores, x => x.Campo == "idRepartidor" && x.Mensaje == "Selecciona un repartidor.");
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_EnviarConRepartidorInexistenteOInactivo_DevuelveError()
    {
        var e = await CrearEscenarioAsync();
        var inactivo = new Repartidor { Nombre = "Inactivo", Celular = "51911111111", Estado = false, EstadoRegistro = true };
        e.Db.Repartidores.Add(inactivo);
        await e.Db.SaveChangesAsync();

        var inexistente = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: 9999), true);
        var deBaja = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: inactivo.Id), true);

        Assert.Contains(inexistente.Errores, x => x.Mensaje == "Selecciona un repartidor.");
        Assert.Contains(deBaja.Errores, x => x.Mensaje == "Selecciona un repartidor.");
    }

    [Theory]
    [InlineData("987654321")]
    [InlineData("51887654321")]
    [InlineData("5198765432")]
    [InlineData("")]
    public async Task CrearAsync_EnviarConCelularInvalido_DevuelveError(string celular)
    {
        var e = await CrearEscenarioAsync();
        e.Repartidor.Celular = celular;
        await e.Db.SaveChangesAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: e.Repartidor.Id), true);

        Assert.Contains(resultado.Errores, x => x.Campo == "idRepartidor"
            && x.Mensaje == "El celular del repartidor no tiene un formato válido.");
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_EnviarValido_QuedaEnviadoYGeneraWhatsapp()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: e.Repartidor.Id), true);

        Assert.True(resultado.Ok);
        Assert.StartsWith("https://wa.me/51987654321", resultado.WhatsappUrl);
        Assert.Contains("Ana Torres", resultado.Mensaje);
        Assert.Equal(EstadosPedido.Enviado, (await e.Db.Pedidos.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CrearAsync_GuardarSinEnviar_IgnoraRepartidorYQuedaPendiente()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.CrearAsync(ModeloValido(e, idRepartidor: e.Repartidor.Id), enviar: false);

        Assert.True(resultado.Ok);
        Assert.Null(resultado.WhatsappUrl);
        var pedido = await e.Db.Pedidos.SingleAsync();
        Assert.Equal(EstadosPedido.Pendiente, pedido.Estado);
        Assert.Null(pedido.IdRepartidor);
    }

    [Fact]
    public async Task PreviewCrearAsync_GeneraMensajeSinPersistir()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.PreviewCrearAsync(ModeloValido(e, idRepartidor: e.Repartidor.Id));

        Assert.True(resultado.Ok);
        Assert.Equal(20m, resultado.Total);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Mensaje));
        Assert.Empty(await e.Db.Pedidos.ToListAsync());
    }

    // ---------- Actualizar ----------

    [Fact]
    public async Task ActualizarAsync_PedidoPendiente_ReemplazaDetallesYTotal()
    {
        var e = await CrearEscenarioAsync();
        var creado = await e.Servicio.CrearAsync(ModeloValido(e, cantidad: 1), false);

        var resultado = await e.Servicio.ActualizarAsync(creado.IdPedido!.Value, ModeloValido(e, cantidad: 4));

        Assert.True(resultado.Ok);
        Assert.Equal(40m, resultado.Total);
        var pedido = await e.Db.Pedidos.Include(p => p.Clientes).ThenInclude(c => c.Detalles).SingleAsync();
        Assert.Equal(40m, pedido.Total);
        Assert.Equal(4, pedido.Clientes.Single().Detalles.Single().Cantidad);
        Assert.Equal(EstadosPedido.Pendiente, pedido.Estado);
    }

    [Fact]
    public async Task ActualizarAsync_PedidoInexistenteONoPendiente_DevuelveError()
    {
        var e = await CrearEscenarioAsync();
        var enviado = new Pedido { Estado = EstadosPedido.Enviado, Total = 10m };
        e.Db.Pedidos.Add(enviado);
        await e.Db.SaveChangesAsync();

        var inexistente = await e.Servicio.ActualizarAsync(9999, ModeloValido(e));
        var noPendiente = await e.Servicio.ActualizarAsync(enviado.Id, ModeloValido(e));

        Assert.Equal("El pedido no existe.", Assert.Single(inexistente.Errores).Mensaje);
        Assert.Equal("Solo se pueden editar pedidos en estado Pendiente.", Assert.Single(noPendiente.Errores).Mensaje);
    }

    // ---------- Productos por local / buscador ----------

    [Fact]
    public async Task ProductosPorLocalAsync_SoloActivosDelLocal()
    {
        var e = await CrearEscenarioAsync();

        var central = await e.Servicio.ProductosPorLocalAsync(e.Local.Id);
        var norte = await e.Servicio.ProductosPorLocalAsync(e.OtroLocal.Id);

        var unico = Assert.Single(central);
        Assert.Equal("Bidón 20L", unico.Nombre);
        Assert.Equal(10m, unico.Precio);
        Assert.Equal(10, unico.Stock);
        Assert.Equal("Hielo 3kg", Assert.Single(norte).Nombre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task BuscarClienteAsync_TerminoVacio_DevuelveNull(string? termino)
    {
        var e = await CrearEscenarioAsync();
        Assert.Null(await e.Servicio.BuscarClienteAsync("nombre", termino!));
    }

    [Fact]
    public async Task BuscarClienteAsync_SinCoincidenciaOClienteInactivo_DevuelveNull()
    {
        var e = await CrearEscenarioAsync();
        e.Cliente2.Estado = false;
        await e.Db.SaveChangesAsync();

        Assert.Null(await e.Servicio.BuscarClienteAsync("nombre", "zzz"));
        Assert.Null(await e.Servicio.BuscarClienteAsync("dni", "87654321"));
        Assert.NotNull(await e.Servicio.BuscarClienteAsync(null!, "ana"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" a ")]
    public async Task BuscarClientes_MenosDeDosCaracteres_DevuelveListaVacia(string? q)
    {
        var e = await CrearEscenarioAsync();
        var controller = ControladorFactory.Preparar(new PedidosController(e.Db, e.Servicio), rol: "Vendedora");

        var json = Assert.IsType<JsonResult>(await controller.BuscarClientes(q!));

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<object>>(json.Value));
    }

    [PostgresFact]
    public async Task BuscarClientes_DesdeDosCaracteres_PorNombreTelefonoODni_Postgres()
    {
        await using var db = PostgresFactAttribute.CrearContexto();
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var sufijo = Guid.NewGuid().ToString("N")[..6];
        db.Clientes.AddRange(
            new Cliente { Nombre = $"Zoila Prueba {sufijo}", Telefono = $"9{sufijo}11", Dni = $"7{sufijo}1" },
            new Cliente { Nombre = $"Inactiva {sufijo}", Telefono = "900000000", Estado = false });
        await db.SaveChangesAsync();

        var controller = ControladorFactory.Preparar(new PedidosController(db, new PedidoService(db)), rol: "Vendedora");
        foreach (var q in new[] { $"zoila prueba {sufijo}", $"9{sufijo}", $"7{sufijo}" })
        {
            var json = Assert.IsType<JsonResult>(await controller.BuscarClientes(q));
            Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(json.Value).Cast<object>());
        }

        var limitado = Assert.IsType<JsonResult>(await controller.BuscarClientes(sufijo, limite: 0));
        Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(limitado.Value).Cast<object>());

        await tx.RollbackAsync();
    }
}
