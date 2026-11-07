using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class HistorialComprasServiceTests
{
    private sealed record Escenario(
        AppDbContext Db,
        HistorialComprasService Servicio,
        Local Local,
        Producto Producto,
        Cliente Cliente,
        DireccionCliente Direccion,
        Cliente OtroCliente,
        DireccionCliente OtraDireccion);

    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var db = CrearContexto();
        var local = new Local { Nombre = "Local Central" };
        var producto = new Producto { Nombre = "Galón 11L", PrecioVenta = 12.50m, Costo = 7.00m };

        var cliente = new Cliente
        {
            Nombre = "Ana Torres",
            Telefono = "999000111",
            Dni = "12345678",
            Estado = true,
            EstadoRegistro = true
        };
        cliente.Direcciones.Add(new DireccionCliente
        {
            Direccion = "Av. Lima 123",
            Ciudad = "Lima",
            Principal = true
        });

        var otroCliente = new Cliente
        {
            Nombre = "Luis Pérez",
            Telefono = "999222333",
            Dni = "87654321",
            Estado = true,
            EstadoRegistro = true
        };
        otroCliente.Direcciones.Add(new DireccionCliente
        {
            Direccion = "Jr. Sol 456",
            Ciudad = "Lima",
            Principal = true
        });

        db.Locales.Add(local);
        db.Productos.Add(producto);
        db.Clientes.AddRange(cliente, otroCliente);
        await db.SaveChangesAsync();

        return new Escenario(
            db,
            new HistorialComprasService(db),
            local,
            producto,
            cliente,
            cliente.Direcciones.Single(),
            otroCliente,
            otroCliente.Direcciones.Single());
    }

    private static PedidoCliente CrearPedidoCliente(
        Escenario e,
        bool esOtroCliente,
        decimal subtotal,
        params DetallePedido[] detalles)
    {
        var cliente = esOtroCliente ? e.OtroCliente : e.Cliente;
        var direccion = esOtroCliente ? e.OtraDireccion : e.Direccion;

        var pedidoCliente = new PedidoCliente
        {
            Cliente = cliente,
            IdCliente = cliente.Id,
            Direccion = direccion,
            IdDireccion = direccion.Id,
            Subtotal = subtotal
        };
        foreach (var detalle in detalles)
            pedidoCliente.Detalles.Add(detalle);

        return pedidoCliente;
    }

    private static DetallePedido CrearDetalle(
        Escenario e, int cantidad, decimal precio, decimal descuento = 0m) => new()
    {
        IdProducto = e.Producto.Id,
        IdLocal = e.Local.Id,
        Cantidad = cantidad,
        PrecioUnitario = precio,
        DescuentoMonto = descuento,
        Subtotal = cantidad * precio - descuento
    };

    private static async Task<Pedido> AgregarPedidoAsync(
        Escenario e,
        DateTime fechaUtc,
        string estado,
        decimal subtotal,
        bool estadoRegistro = true,
        params DetallePedido[] detalles)
    {
        var pedido = new Pedido
        {
            Estado = estado,
            Total = subtotal,
            EstadoRegistro = estadoRegistro,
            FechaCreacion = fechaUtc
        };
        pedido.Clientes.Add(CrearPedidoCliente(e, false, subtotal, detalles));

        e.Db.Pedidos.Add(pedido);
        await e.Db.SaveChangesAsync();
        return pedido;
    }

    [Fact]
    public async Task ObtenerAsync_ClienteSinPedidos_DevuelveResumenVacio()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        Assert.Equal(0m, resultado.TotalGastado);
        Assert.Equal(0, resultado.CantidadPedidos);
        Assert.Null(resultado.FechaUltimaCompra);
        Assert.False(resultado.EsClienteFrecuente);
        Assert.Empty(resultado.Pedidos);
        Assert.Null(resultado.ErrorFiltro);
    }

    [Fact]
    public async Task ObtenerAsync_RangoInvalido_DevuelveErrorYNoConsultaPedidos()
    {
        var e = await CrearEscenarioAsync();
        await AgregarPedidoAsync(e, new DateTime(2026, 11, 3, 15, 0, 0), EstadosPedido.Entregado, 20m);

        var resultado = await e.Servicio.ObtenerAsync(
            e.Cliente.Id,
            desde: new DateTime(2026, 11, 10),
            hasta: new DateTime(2026, 11, 1));

        Assert.Equal("La fecha inicial no puede ser posterior a la fecha final.", resultado.ErrorFiltro);
        Assert.Empty(resultado.Pedidos);
        Assert.Equal(0m, resultado.TotalGastado);
        Assert.Equal(new DateTime(2026, 11, 10), resultado.FechaDesde);
        Assert.Equal(new DateTime(2026, 11, 1), resultado.FechaHasta);
    }

    [Fact]
    public async Task ObtenerAsync_SoloCuentanEstadosValidosEnElResumenPeroTodosSeListan()
    {
        var e = await CrearEscenarioAsync();
        var baseFecha = new DateTime(2026, 11, 1, 15, 0, 0);
        await AgregarPedidoAsync(e, baseFecha.AddDays(0), EstadosPedido.Enviado, 10m);
        await AgregarPedidoAsync(e, baseFecha.AddDays(1), EstadosPedido.Entregado, 20m);
        await AgregarPedidoAsync(e, baseFecha.AddDays(2), EstadosPedido.Pagado, 30m);
        await AgregarPedidoAsync(e, baseFecha.AddDays(3), EstadosPedido.PagoParcial, 40m);
        await AgregarPedidoAsync(e, baseFecha.AddDays(4), EstadosPedido.Pendiente, 500m);
        await AgregarPedidoAsync(e, baseFecha.AddDays(5), EstadosPedido.ConIncidencia, 600m);

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        Assert.Equal(100m, resultado.TotalGastado);
        Assert.Equal(4, resultado.CantidadPedidos);
        Assert.Equal(6, resultado.Pedidos.Count);
        // La última compra que cuenta es la del día 3 (Pago Parcial), no las de Pendiente/Con Incidencia
        Assert.Equal(baseFecha.AddDays(3).AddHours(-5), resultado.FechaUltimaCompra);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public async Task ObtenerAsync_ClienteFrecuenteConCuatroOMasPedidos(int cantidad, bool esperado)
    {
        var e = await CrearEscenarioAsync();
        for (var i = 0; i < cantidad; i++)
            await AgregarPedidoAsync(e, new DateTime(2026, 11, 1, 15, 0, 0).AddDays(i), EstadosPedido.Pagado, 10m);

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        Assert.Equal(cantidad, resultado.CantidadPedidos);
        Assert.Equal(esperado, resultado.EsClienteFrecuente);
    }

    [Fact]
    public async Task ObtenerAsync_IncluyeProductosConCantidadPrecioDescuentoYSubtotal()
    {
        var e = await CrearEscenarioAsync();
        await AgregarPedidoAsync(
            e,
            new DateTime(2026, 11, 3, 15, 0, 0),
            EstadosPedido.Entregado,
            23.50m,
            true,
            CrearDetalle(e, cantidad: 2, precio: 12.50m, descuento: 1.50m));

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        var pedido = Assert.Single(resultado.Pedidos);
        var producto = Assert.Single(pedido.Productos);
        Assert.Equal("Galón 11L", producto.Producto);
        Assert.Equal(2, producto.Cantidad);
        Assert.Equal(12.50m, producto.PrecioUnitario);
        Assert.Equal(1.50m, producto.DescuentoMonto);
        Assert.Equal(23.50m, producto.Subtotal);
        Assert.Equal(23.50m, pedido.Monto);
        Assert.Equal(EstadosPedido.Entregado, pedido.Estado);
    }

    [Fact]
    public async Task ObtenerAsync_PedidoSinDetalles_DevuelveListaDeProductosVacia()
    {
        var e = await CrearEscenarioAsync();
        await AgregarPedidoAsync(e, new DateTime(2026, 11, 3, 15, 0, 0), EstadosPedido.Entregado, 15m);

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        var pedido = Assert.Single(resultado.Pedidos);
        Assert.Empty(pedido.Productos);
    }

    [Fact]
    public async Task ObtenerAsync_FiltraPorFechaDePeruNoPorFechaUtc()
    {
        var e = await CrearEscenarioAsync();
        var antes = await AgregarPedidoAsync(e, new DateTime(2026, 11, 2, 15, 0, 0), EstadosPedido.Pagado, 10m);
        // 03:00 UTC del 6 de noviembre = 22:00 del 5 de noviembre en Perú
        var borde = await AgregarPedidoAsync(e, new DateTime(2026, 11, 6, 3, 0, 0), EstadosPedido.Pagado, 20m);
        var despues = await AgregarPedidoAsync(e, new DateTime(2026, 11, 10, 15, 0, 0), EstadosPedido.Pagado, 30m);

        var dia5 = await e.Servicio.ObtenerAsync(
            e.Cliente.Id, new DateTime(2026, 11, 5), new DateTime(2026, 11, 5));
        var pedidoDia5 = Assert.Single(dia5.Pedidos);
        Assert.Equal(borde.Id, pedidoDia5.IdPedido);
        Assert.Equal(20m, dia5.TotalGastado);
        Assert.Equal(1, dia5.CantidadPedidos);

        var desdeDia6 = await e.Servicio.ObtenerAsync(e.Cliente.Id, desde: new DateTime(2026, 11, 6));
        var pedidoDesdeDia6 = Assert.Single(desdeDia6.Pedidos);
        Assert.Equal(despues.Id, pedidoDesdeDia6.IdPedido);

        var hastaDia2 = await e.Servicio.ObtenerAsync(e.Cliente.Id, hasta: new DateTime(2026, 11, 2));
        var pedidoHastaDia2 = Assert.Single(hastaDia2.Pedidos);
        Assert.Equal(antes.Id, pedidoHastaDia2.IdPedido);
    }

    [Fact]
    public async Task ObtenerAsync_FiltroConFechasConHora_GuardaSoloLaFecha()
    {
        var e = await CrearEscenarioAsync();

        var resultado = await e.Servicio.ObtenerAsync(
            e.Cliente.Id,
            new DateTime(2026, 11, 1, 13, 45, 0),
            new DateTime(2026, 11, 6, 8, 30, 0));

        Assert.Equal(new DateTime(2026, 11, 1), resultado.FechaDesde);
        Assert.Equal(new DateTime(2026, 11, 6), resultado.FechaHasta);
        Assert.Null(resultado.ErrorFiltro);
    }

    [Fact]
    public async Task ObtenerAsync_OrdenaDelMasRecienteAlMasAntiguoYDesempataPorNumero()
    {
        var e = await CrearEscenarioAsync();
        var antiguo = await AgregarPedidoAsync(e, new DateTime(2026, 11, 1, 15, 0, 0), EstadosPedido.Pagado, 10m);
        var mismoMomento = new DateTime(2026, 11, 5, 15, 0, 0);
        var empateMenor = await AgregarPedidoAsync(e, mismoMomento, EstadosPedido.Pagado, 20m);
        var empateMayor = await AgregarPedidoAsync(e, mismoMomento, EstadosPedido.Pagado, 30m);

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        Assert.Equal(
            new[] { empateMayor.Id, empateMenor.Id, antiguo.Id },
            resultado.Pedidos.Select(p => p.IdPedido).ToArray());
        Assert.Equal(mismoMomento.AddHours(-5), resultado.Pedidos[0].Fecha);
    }

    [Fact]
    public async Task ObtenerAsync_IgnoraPedidosInactivosYMontosDeOtrosClientes()
    {
        var e = await CrearEscenarioAsync();
        var fecha = new DateTime(2026, 11, 3, 15, 0, 0);

        // Pedido compartido: solo debe contar la parte de este cliente
        var compartido = new Pedido
        {
            Estado = EstadosPedido.Pagado,
            Total = 50m,
            FechaCreacion = fecha
        };
        compartido.Clientes.Add(CrearPedidoCliente(e, false, 20m, CrearDetalle(e, 1, 20m)));
        compartido.Clientes.Add(CrearPedidoCliente(e, true, 30m, CrearDetalle(e, 3, 10m)));
        e.Db.Pedidos.Add(compartido);

        // Pedido dado de baja
        await AgregarPedidoAsync(e, fecha.AddDays(1), EstadosPedido.Pagado, 100m, estadoRegistro: false);

        // Pedido solo del otro cliente
        var soloOtro = new Pedido
        {
            Estado = EstadosPedido.Pagado,
            Total = 70m,
            FechaCreacion = fecha.AddDays(2)
        };
        soloOtro.Clientes.Add(CrearPedidoCliente(e, true, 70m));
        e.Db.Pedidos.Add(soloOtro);

        await e.Db.SaveChangesAsync();

        var resultado = await e.Servicio.ObtenerAsync(e.Cliente.Id);

        var pedido = Assert.Single(resultado.Pedidos);
        Assert.Equal(compartido.Id, pedido.IdPedido);
        Assert.Equal(20m, pedido.Monto);
        Assert.Equal(20m, resultado.TotalGastado);
        var producto = Assert.Single(pedido.Productos);
        Assert.Equal(1, producto.Cantidad);
    }
}