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

    private static async Task<(AppDbContext Db, PedidoService Servicio, Local Local, Producto Producto, Cliente Cliente, DireccionCliente Direccion, Repartidor Repartidor)> CrearEscenarioAsync()
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
            Principal = true,
            UrlUbicacion = "https://maps.example/ana"
        });

        var repartidor = new Repartidor
        {
            Nombre = "Carlos Ramos",
            Celular = "51987654321",
            Estado = true,
            EstadoRegistro = true
        };

        db.Locales.Add(local);
        db.Productos.Add(producto);
        db.Clientes.Add(cliente);
        db.Repartidores.Add(repartidor);
        await db.SaveChangesAsync();

        var direccion = cliente.Direcciones.Single();
        db.ProductosLocal.Add(new ProductoLocal
        {
            IdLocal = local.Id,
            IdProducto = producto.Id,
            Stock = 25,
            Estado = true,
            EstadoRegistro = true
        });
        await db.SaveChangesAsync();

        return (db, new PedidoService(db), local, producto, cliente, direccion, repartidor);
    }

    [Fact]
    public async Task CrearAsync_ConDatosValidos_GuardaPedidoYCalculaTotal()
    {
        var (db, servicio, local, producto, cliente, direccion, repartidor) = await CrearEscenarioAsync();

        var modelo = new CrearPedidoViewModel
        {
            IdRepartidor = repartidor.Id,
            Clientes =
            {
                new PedidoClienteViewModel
                {
                    IdCliente = cliente.Id,
                    IdDireccion = direccion.Id,
                    Detalles =
                    {
                        new DetallePedidoViewModel
                        {
                            IdLocal = local.Id,
                            IdProducto = producto.Id,
                            Cantidad = 2,
                            DescuentoMonto = 1.50m
                        }
                    }
                }
            }
        };

        var resultado = await servicio.CrearAsync(modelo, enviar: false);

        Assert.True(resultado.Ok, string.Join("; ", resultado.Errores.Select(e => e.Mensaje)));
        Assert.NotNull(resultado.IdPedido);
        Assert.Equal(23.50m, resultado.Total);

        var pedidoGuardado = await db.Pedidos.Include(p => p.Clientes).ThenInclude(c => c.Detalles).SingleAsync();
        Assert.Equal(EstadosPedido.Pendiente, pedidoGuardado.Estado);
        Assert.Equal(23.50m, pedidoGuardado.Total);
    }

    [Fact]
    public async Task CrearAsync_ConDescuentoMayorQueSubtotal_DevuelveError()
    {
        var (db, servicio, local, producto, cliente, direccion, repartidor) = await CrearEscenarioAsync();

        var modelo = new CrearPedidoViewModel
        {
            IdRepartidor = repartidor.Id,
            Clientes =
            {
                new PedidoClienteViewModel
                {
                    IdCliente = cliente.Id,
                    IdDireccion = direccion.Id,
                    Detalles =
                    {
                        new DetallePedidoViewModel
                        {
                            IdLocal = local.Id,
                            IdProducto = producto.Id,
                            Cantidad = 1,
                            DescuentoMonto = 100m
                        }
                    }
                }
            }
        };

        var resultado = await servicio.CrearAsync(modelo, enviar: false);

        Assert.False(resultado.Ok);
        Assert.Contains(resultado.Errores, e => e.Campo == "clientes[0].detalles[0].descuentoMonto");
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesdePendiente_AceptaSoloEnviado()
    {
        var (db, servicio, _, _, _, _, _) = await CrearEscenarioAsync();
        var pedido = new Pedido
        {
            Estado = EstadosPedido.Pendiente,
            EstadoRegistro = true,
            Total = 20m
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var resultadoInvalido = await servicio.CambiarEstadoAsync(pedido.Id, EstadosPedido.Entregado);
        Assert.False(resultadoInvalido.Ok);
        Assert.Contains(resultadoInvalido.Errores, e => e.Campo == "estado");

        var resultadoValido = await servicio.CambiarEstadoAsync(pedido.Id, EstadosPedido.Enviado);
        Assert.True(resultadoValido.Ok);
        Assert.Equal(EstadosPedido.Enviado, (await db.Pedidos.SingleAsync()).Estado);
    }

    [Theory]
    [InlineData("telefono", "999000111")]
    [InlineData("dni", "12345678")]
    [InlineData("NOMBRE", "ana")]
    [InlineData("otro", "ANA")]
    public async Task BuscarClienteAsync_PorCriterio_DevuelveClienteYDirecciones(string tipo, string termino)
    {
        var (_, servicio, _, _, cliente, direccion, _) = await CrearEscenarioAsync();

        var resultado = await servicio.BuscarClienteAsync(tipo, termino);

        Assert.NotNull(resultado);
        Assert.Equal(cliente.Nombre, resultado!.Nombre);
        Assert.Contains(resultado.Direcciones, d => d.Id == direccion.Id);
    }
}

