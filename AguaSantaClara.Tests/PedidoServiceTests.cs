using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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

    [Fact]
    public async Task RegistrarPagosAsync_PermiteDividirElPagoDelClientePorMetodoYValidaElTotal()
    {
        var (db, servicio, _, _, cliente, direccion, _) = await CrearEscenarioAsync();
        var metodoPago = new MetodoPago { Nombre = "Efectivo" };
        var segundoMetodoPago = new MetodoPago { Nombre = "Yape" };
        var pedido = new Pedido
        {
            Estado = EstadosPedido.Entregado,
            Total = 20m,
            Clientes =
            {
                new PedidoCliente
                {
                    Cliente = cliente,
                    IdCliente = cliente.Id,
                    Direccion = direccion,
                    IdDireccion = direccion.Id,
                    Subtotal = 20m
                }
            }
        };
        db.MetodosPago.Add(metodoPago);
        db.MetodosPago.Add(segundoMetodoPago);
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var pagoParcial = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 6.50m
                },
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = segundoMetodoPago.Id,
                    Monto = 2m
                }
            }
        });

        Assert.True(pagoParcial.Ok);
        var pagosGuardados = await db.Pagos.OrderBy(p => p.IdMetodoPago).ToListAsync();
        Assert.Equal(2, pagosGuardados.Count);
        Assert.All(pagosGuardados, pago =>
        {
            Assert.Equal(pedido.Id, pago.IdPedido);
            Assert.Equal(cliente.Id, pago.IdCliente);
        });
        Assert.Equal(metodoPago.Id, pagosGuardados[0].IdMetodoPago);
        Assert.Equal(6.50m, pagosGuardados[0].Monto);
        Assert.Equal(segundoMetodoPago.Id, pagosGuardados[1].IdMetodoPago);
        Assert.Equal(2m, pagosGuardados[1].Monto);
        Assert.Equal(11.50m, cliente.DeudaTotal);
        Assert.Equal(EstadosPedido.PagoParcial, pedido.Estado);
        var deuda = await db.Deudas.SingleAsync();
        Assert.Equal(20m, deuda.Monto);
        Assert.Equal(EstadosDeuda.Pendiente, deuda.Estado);

        var excedePendiente = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 6m
                },
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = segundoMetodoPago.Id,
                    Monto = 6m
                }
            }
        });

        Assert.False(excedePendiente.Ok);
        Assert.Contains(excedePendiente.Errores, error => error.Campo == "pagos");
        Assert.Equal(2, await db.Pagos.CountAsync());

        var pagoRestante = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 11.50m
                }
            }
        });

        Assert.True(pagoRestante.Ok);
        Assert.Equal(0m, cliente.DeudaTotal);
        Assert.Equal(EstadosDeuda.Pagada, deuda.Estado);
        Assert.Equal(EstadosPedido.Pagado, pedido.Estado);
        Assert.Equal(3, await db.Pagos.CountAsync());

        var pagoPedidoCancelado = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 1m
                }
            }
        });

        Assert.False(pagoPedidoCancelado.Ok);
        Assert.Contains(pagoPedidoCancelado.Errores, error => error.Campo == "pedido");
        Assert.Equal(3, await db.Pagos.CountAsync());
    }

    [Fact]
    public async Task RegistrarPagosAsync_AcumulaLaDeudaPendientePorClienteIndependientemente()
    {
        var (db, servicio, _, _, cliente, direccion, _) = await CrearEscenarioAsync();
        var segundoCliente = new Cliente
        {
            Nombre = "Luis Pérez",
            Telefono = "999222333",
            Dni = "87654321",
            DeudaTotal = 4m
        };
        var segundaDireccion = new DireccionCliente
        {
            Cliente = segundoCliente,
            Direccion = "Jr. Sol 456",
            Ciudad = "Lima"
        };
        db.Clientes.Add(segundoCliente);
        await db.SaveChangesAsync();

        var metodoPago = new MetodoPago { Nombre = "Yape" };
        var pedido = new Pedido
        {
            Estado = EstadosPedido.Entregado,
            Total = 50m,
            Clientes =
            {
                new PedidoCliente
                {
                    Cliente = cliente,
                    IdCliente = cliente.Id,
                    Direccion = direccion,
                    IdDireccion = direccion.Id,
                    Subtotal = 20m
                },
                new PedidoCliente
                {
                    Cliente = segundoCliente,
                    IdCliente = segundoCliente.Id,
                    Direccion = segundaDireccion,
                    IdDireccion = segundaDireccion.Id,
                    Subtotal = 30m
                }
            }
        };
        db.MetodosPago.Add(metodoPago);
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var resultado = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 5m
                }
            }
        });

        Assert.True(resultado.Ok);
        Assert.Equal(15m, cliente.DeudaTotal);
        Assert.Equal(34m, segundoCliente.DeudaTotal);
        Assert.Equal(EstadosPedido.PagoParcial, pedido.Estado);
        Assert.Collection(
            await db.Deudas.OrderBy(d => d.IdCliente).ToListAsync(),
            deuda =>
            {
                Assert.Equal(cliente.Id, deuda.IdCliente);
                Assert.Equal(20m, deuda.Monto);
                Assert.Equal(EstadosDeuda.Pendiente, deuda.Estado);
            },
            deuda =>
            {
                Assert.Equal(segundoCliente.Id, deuda.IdCliente);
                Assert.Equal(30m, deuda.Monto);
                Assert.Equal(EstadosDeuda.Pendiente, deuda.Estado);
            });

        var saldosRestantes = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 15m
                },
                new PagoClientePedidoViewModel
                {
                    IdCliente = segundoCliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 30m
                }
            }
        });

        Assert.True(saldosRestantes.Ok);
        Assert.Equal(EstadosPedido.Pagado, pedido.Estado);
        Assert.Equal(0m, cliente.DeudaTotal);
        Assert.Equal(4m, segundoCliente.DeudaTotal);
        Assert.All(await db.Deudas.ToListAsync(), deuda => Assert.Equal(EstadosDeuda.Pagada, deuda.Estado));
    }

    [Fact]
    public async Task PedidosController_Index_IncluyePagosDePedidosParcialesParaMostrarSaldoPendiente()
    {
        await using var db = CrearContexto();
        var metodoPago = new MetodoPago { Nombre = "Efectivo" };
        var cliente = new Cliente
        {
            Nombre = "Ana Torres",
            Telefono = "999111222"
        };
        var direccion = new DireccionCliente
        {
            Cliente = cliente,
            Direccion = "Av. Lima 123"
        };
        var pedido = new Pedido
        {
            Estado = EstadosPedido.PagoParcial,
            Total = 50m,
            Clientes =
            {
                new PedidoCliente
                {
                    Cliente = cliente,
                    IdCliente = cliente.Id,
                    Direccion = direccion,
                    IdDireccion = direccion.Id,
                    Subtotal = 50m
                }
            }
        };
        db.MetodosPago.Add(metodoPago);
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        db.Pagos.Add(new Pago
        {
            Pedido = pedido,
            IdPedido = pedido.Id,
            Cliente = cliente,
            IdCliente = cliente.Id,
            MetodoPago = metodoPago,
            IdMetodoPago = metodoPago.Id,
            Monto = 17m
        });
        await db.SaveChangesAsync();

        var controller = new PedidosController(db, new PedidoService(db));
        var resultado = Assert.IsType<ViewResult>(await controller.Index(null, null, null));
        var modelo = Assert.IsType<PedidosIndexViewModel>(resultado.Model);

        Assert.Equal(17m, modelo.MontosPagadosPorPedidoCliente[pedido.Id][cliente.Id]);
    }

    [Fact]
    public async Task RegistrarPagosAsync_RechazaPedidoNoEntregado()
    {
        var (db, servicio, _, _, cliente, direccion, _) = await CrearEscenarioAsync();
        var metodoPago = new MetodoPago { Nombre = "Efectivo" };
        var pedido = new Pedido
        {
            Estado = EstadosPedido.Enviado,
            Total = 20m,
            Clientes =
            {
                new PedidoCliente
                {
                    Cliente = cliente,
                    IdCliente = cliente.Id,
                    Direccion = direccion,
                    IdDireccion = direccion.Id,
                    Subtotal = 20m
                }
            }
        };
        db.MetodosPago.Add(metodoPago);
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var resultado = await servicio.RegistrarPagosAsync(pedido.Id, new RegistrarPagosPedidoViewModel
        {
            Pagos =
            {
                new PagoClientePedidoViewModel
                {
                    IdCliente = cliente.Id,
                    IdMetodoPago = metodoPago.Id,
                    Monto = 5m
                }
            }
        });

        Assert.False(resultado.Ok);
        Assert.Contains(resultado.Errores, error => error.Campo == "pedido");
        Assert.Empty(await db.Pagos.ToListAsync());
    }

    [Fact]
    public void ModeloPedido_AceptaEstadosGeneradosPorElRegistroDePagos()
    {
        using var db = CrearContexto();
        var restriccion = db.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Pedido))!
            .GetCheckConstraints()
            .Single(constraint => constraint.Name == "chk_pedido_estado");

        Assert.Contains(EstadosPedido.Pagado, restriccion.Sql);
        Assert.Contains(EstadosPedido.PagoParcial, restriccion.Sql);
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
