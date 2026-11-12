using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class RentabilidadServiceTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateTime AhoraUtc = DateTime.UtcNow;

    private sealed class Escenario
    {
        public required AppDbContext Db { get; init; }
        public required RentabilidadService Servicio { get; init; }
        public required Local Local { get; init; }
        public required Cliente Cliente { get; init; }
        public required DireccionCliente Direccion { get; init; }

        public async Task<Producto> ProductoAsync(string nombre, string? categoria, decimal precio, decimal costo, bool activo = true)
        {
            var producto = new Producto
            {
                Nombre = nombre, Categoria = categoria, PrecioVenta = precio, Costo = costo, EstadoRegistro = activo
            };
            Db.Productos.Add(producto);
            await Db.SaveChangesAsync();
            return producto;
        }

        public DetallePedido Detalle(Producto producto, int cantidad, decimal precio, decimal descuento = 0m, bool activo = true) => new()
        {
            IdProducto = producto.Id,
            IdLocal = Local.Id,
            Cantidad = cantidad,
            PrecioUnitario = precio,
            DescuentoMonto = descuento,
            Subtotal = cantidad * precio - descuento,
            EstadoRegistro = activo
        };

        public async Task PedidoAsync(DateTime fechaUtc, params DetallePedido[] detalles) =>
            await PedidoAsync(fechaUtc, true, true, detalles);

        public async Task PedidoAsync(DateTime fechaUtc, bool pedidoActivo, bool pedidoClienteActivo, params DetallePedido[] detalles)
        {
            var subtotal = detalles.Sum(d => d.Subtotal);
            var pedidoCliente = new PedidoCliente
            {
                IdCliente = Cliente.Id,
                IdDireccion = Direccion.Id,
                Subtotal = subtotal,
                EstadoRegistro = pedidoClienteActivo
            };
            foreach (var detalle in detalles)
                pedidoCliente.Detalles.Add(detalle);

            var pedido = new Pedido
            {
                Estado = EstadosPedido.Entregado,
                Total = subtotal,
                FechaCreacion = fechaUtc,
                EstadoRegistro = pedidoActivo
            };
            pedido.Clientes.Add(pedidoCliente);

            Db.Pedidos.Add(pedido);
            await Db.SaveChangesAsync();
        }
    }

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var local = new Local { Nombre = "Local Central" };
        var cliente = new Cliente { Nombre = "Ana Torres", Telefono = "999000111", Dni = "12345678" };
        cliente.Direcciones.Add(new DireccionCliente { Direccion = "Av. Lima 123", Ciudad = "Lima", Principal = true });
        db.Locales.Add(local);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        return new Escenario
        {
            Db = db,
            Servicio = new RentabilidadService(db),
            Local = local,
            Cliente = cliente,
            Direccion = cliente.Direcciones.Single()
        };
    }

    private static LineaRentabilidadViewModel Linea(RentabilidadViewModel vm, string nombre) =>
        vm.Lineas.Single(l => l.Nombre == nombre);

    [Fact]
    public async Task Descuento_SeRestaAntesDelMargen()
    {
        var e = await CrearEscenarioAsync();
        var bidon = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 4m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(bidon, 3, 10m, descuento: 5m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var producto = Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single();
        Assert.Equal(30m, producto.IngresoBruto);
        Assert.Equal(5m, producto.Descuentos);
        Assert.Equal(25m, producto.IngresoNeto);
        Assert.Equal(12m, producto.Costo);
        Assert.Equal(13m, producto.Margen);
        Assert.Equal(52m, producto.PorcentajeMargen);
    }

    [Fact]
    public async Task IngresoNetoCero_PorcentajeEsNulo()
    {
        var e = await CrearEscenarioAsync();
        var hielo = await e.ProductoAsync("Hielo 3kg", "Hielo", 5m, 2m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(hielo, 2, 5m, descuento: 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var linea = Linea(vm, RentabilidadService.LineaHielo);
        Assert.Equal(0m, linea.IngresoNeto);
        Assert.Equal(-4m, linea.Margen);
        Assert.Null(linea.PorcentajeMargen);
        Assert.Null(linea.Productos.Single().PorcentajeMargen);
    }

    [Fact]
    public async Task SinVentas_MuestraLasTresLineasEnCero()
    {
        var e = await CrearEscenarioAsync();

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Equal(
            new[] { RentabilidadService.LineaBidonesGalones, RentabilidadService.LineaPapel, RentabilidadService.LineaHielo },
            vm.Lineas.Select(l => l.Nombre));
        Assert.All(vm.Lineas, l =>
        {
            Assert.Equal(0m, l.IngresoNeto);
            Assert.Null(l.PorcentajeMargen);
            Assert.Empty(l.Productos);
        });
        Assert.Null(vm.PorcentajeMargen);
    }

    [Fact]
    public async Task CostoMayorQueIngreso_MarcaMargenNegativo()
    {
        var e = await CrearEscenarioAsync();
        var papel = await e.ProductoAsync("Papel x4", "Papel", 6m, 8m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(papel, 2, 6m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var linea = Linea(vm, RentabilidadService.LineaPapel);
        var producto = linea.Productos.Single();
        Assert.Equal(-4m, producto.Margen);
        Assert.True(producto.MargenNegativo);
        Assert.True(linea.MargenNegativo);
        Assert.Equal(-33.33m, Math.Round(producto.PorcentajeMargen!.Value, 2));
    }

    [Fact]
    public async Task BidonYGalon_SeAgrupanEnLaMismaLinea()
    {
        var e = await CrearEscenarioAsync();
        var bidon = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 4m);
        var galon = await e.ProductoAsync("Galón 7L", "Galón", 6m, 3m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(bidon, 2, 10m), e.Detalle(galon, 1, 6m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var linea = Linea(vm, RentabilidadService.LineaBidonesGalones);
        Assert.Equal(2, linea.Productos.Count);
        Assert.Equal(26m, linea.IngresoNeto);
        Assert.Equal(11m, linea.Costo);
        Assert.Equal(15m, linea.Margen);
        Assert.Equal(26m, vm.IngresoNeto);
        Assert.Equal(15m, vm.Margen);
    }

    [Fact]
    public async Task PapelHigienicoLegado_VaALaLineaPapel()
    {
        var e = await CrearEscenarioAsync();
        var papel = await e.ProductoAsync("Papel legado", "Papel Higiénico", 6m, 3m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(papel, 1, 6m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Single(Linea(vm, RentabilidadService.LineaPapel).Productos);
        Assert.Empty(vm.CategoriasSinLinea);
    }

    [Fact]
    public async Task CategoriaDesconocida_VaAOtrosYSeReporta()
    {
        var e = await CrearEscenarioAsync();
        var vaso = await e.ProductoAsync("Vaso", "Vasos", 1m, 0.5m);
        var sinCategoria = await e.ProductoAsync("Sin categoría", null, 2m, 1m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(vaso, 1, 1m), e.Detalle(sinCategoria, 1, 2m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Equal(RentabilidadService.LineaOtros, vm.Lineas.Last().Nombre);
        Assert.Equal(2, Linea(vm, RentabilidadService.LineaOtros).Productos.Count);
        Assert.Equal(new[] { "(sin categoría)", "Vasos" }, vm.CategoriasSinLinea);
    }

    [Fact]
    public async Task PedidoFueraDeRango_SeExcluye()
    {
        var e = await CrearEscenarioAsync();
        var bidon = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 4m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(bidon, 1, 10m));
        await e.PedidoAsync(AhoraUtc.AddDays(-40), e.Detalle(bidon, 5, 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var producto = Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single();
        Assert.Equal(1, producto.Cantidad);
        Assert.Equal(10m, producto.IngresoNeto);
    }

    [Fact]
    public async Task RangoAmplio_IncluyeVentasDeDiasAnteriores()
    {
        var e = await CrearEscenarioAsync();
        var bidon = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 4m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(bidon, 1, 10m));
        await e.PedidoAsync(AhoraUtc.AddDays(-40), e.Detalle(bidon, 5, 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy.AddDays(-45), Hoy);

        Assert.Equal(6, Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single().Cantidad);
    }

    [Fact]
    public async Task PedidoPedidoClienteODetalleInactivos_SeExcluyen()
    {
        var e = await CrearEscenarioAsync();
        var bidon = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 4m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(bidon, 1, 10m), e.Detalle(bidon, 7, 10m, activo: false));
        await e.PedidoAsync(AhoraUtc, pedidoActivo: false, pedidoClienteActivo: true, e.Detalle(bidon, 3, 10m));
        await e.PedidoAsync(AhoraUtc, pedidoActivo: true, pedidoClienteActivo: false, e.Detalle(bidon, 4, 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Equal(1, Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single().Cantidad);
    }

    [Fact]
    public async Task ProductoDadoDeBaja_ConVentasEnElPeriodo_SeIncluyeParaCuadrarConVentas()
    {
        var e = await CrearEscenarioAsync();
        var inactivo = await e.ProductoAsync("Bidón descontinuado", "Bidón", 10m, 4m, activo: false);
        await e.PedidoAsync(AhoraUtc, e.Detalle(inactivo, 2, 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Equal(20m, Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single().IngresoNeto);
        Assert.Equal(20m, vm.IngresoNeto);
    }

    [Fact]
    public async Task UsaCostoActualDelProductoYPrecioDeCatalogo()
    {
        var e = await CrearEscenarioAsync();
        var galon = await e.ProductoAsync("Galón 7L", "Galón", 7m, 3m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(galon, 2, 6m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        var producto = Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Single();
        Assert.Equal(7m, producto.PrecioVenta);
        Assert.Equal(3m, producto.CostoUnitario);
        Assert.Equal(12m, producto.IngresoNeto);
        Assert.Equal(6m, producto.Costo);
    }

    [Fact]
    public async Task ProductosDeLaLinea_OrdenadosPorMargenDescendente()
    {
        var e = await CrearEscenarioAsync();
        var menor = await e.ProductoAsync("Galón 7L", "Galón", 6m, 5m);
        var mayor = await e.ProductoAsync("Bidón 20L", "Bidón", 10m, 2m);
        await e.PedidoAsync(AhoraUtc, e.Detalle(menor, 1, 6m), e.Detalle(mayor, 1, 10m));

        var vm = await e.Servicio.ObtenerRentabilidadAsync(Hoy, Hoy);

        Assert.Equal(new[] { "Bidón 20L", "Galón 7L" },
            Linea(vm, RentabilidadService.LineaBidonesGalones).Productos.Select(p => p.Nombre));
    }
}
