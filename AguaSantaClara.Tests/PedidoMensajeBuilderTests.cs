using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;

namespace AguaSantaClara.Tests;

public class PedidoMensajeBuilderTests
{
    private static Cliente CrearCliente(long id, string nombre)
        => new() { Id = id, Nombre = nombre, Telefono = "999111222" };

    private static DireccionCliente CrearDireccion(long id, string direccion, string? ciudad = null, string? url = null)
        => new() { Id = id, Direccion = direccion, Ciudad = ciudad, UrlUbicacion = url };

    private static PedidoCliente CrearLinea(Cliente cliente, DireccionCliente direccion, params (string Producto, int Cantidad, decimal Precio)[] items)
    {
        var linea = new PedidoCliente
        {
            IdCliente = cliente.Id,
            Cliente = cliente,
            IdDireccion = direccion.Id,
            Direccion = direccion,
        };

        foreach (var (producto, cantidad, precio) in items)
        {
            var detalle = new DetallePedido
            {
                Producto = new Producto { Nombre = producto, PrecioVenta = precio },
                Cantidad = cantidad,
                PrecioUnitario = precio,
                DescuentoMonto = 0m,
                Subtotal = cantidad * precio
            };
            linea.Detalles.Add(detalle);
        }

        linea.Subtotal = linea.Detalles.Sum(d => d.Subtotal);
        return linea;
    }

    private static Pedido CrearPedido(long id, params PedidoCliente[] lineas)
    {
        var pedido = new Pedido
        {
            Id = id,
            Estado = EstadosPedido.Pendiente,
            Total = lineas.Sum(l => l.Subtotal),
            FechaCreacion = DateTime.SpecifyKind(new DateTime(2026, 10, 5, 14, 30, 0), DateTimeKind.Local).ToUniversalTime()
        };

        foreach (var linea in lineas)
            pedido.Clientes.Add(linea);

        return pedido;
    }

    [Fact]
    public void Construir_IncluyeDatosPrincipalesDelPedido()
    {
        var cliente = CrearCliente(1, "Ana Torres");
        var direccion = CrearDireccion(1, "Av. Lima 123", "Lima", "https://maps.example/ana");
        var pedido = CrearPedido(42,
            CrearLinea(cliente, direccion, ("Galón 11L", 2, 12.50m)));

        var mensaje = PedidoMensajeBuilder.Construir(pedido);

        Assert.Contains("PEDIDO N° 42", mensaje);
        Assert.Contains("Ana Torres", mensaje);
        Assert.Contains("Av. Lima 123, Lima", mensaje);
        Assert.Contains("https://maps.example/ana", mensaje);
        Assert.Contains("2 x Galón 11L", mensaje);
        Assert.Contains("TOTAL:", mensaje);
    }

    [Fact]
    public void Construir_SinUrlDeUbicacion_NoIncluyeLaLineaUbicacion()
    {
        var cliente = CrearCliente(1, "Ana Torres");
        var direccion = CrearDireccion(1, "Av. Lima 123", "Lima");
        var pedido = CrearPedido(10, CrearLinea(cliente, direccion, ("Galón 11L", 1, 10m)));

        var mensaje = PedidoMensajeBuilder.Construir(pedido);

        Assert.DoesNotContain("Ubicación:", mensaje);
    }

    [Fact]
    public void ConstruirVarios_ConcatenaPedidosYTotalGeneral()
    {
        var cliente = CrearCliente(1, "Ana Torres");
        var direccion = CrearDireccion(1, "Av. Lima 123", "Lima");

        var pedido1 = CrearPedido(42, CrearLinea(cliente, direccion, ("Galón 11L", 2, 12.50m)));
        var pedido2 = CrearPedido(43, CrearLinea(cliente, direccion, ("Bolsa de hielo", 1, 5m)));

        var mensaje = PedidoMensajeBuilder.ConstruirVarios(new[] { pedido1, pedido2 });

        Assert.Contains("PEDIDOS ASIGNADOS: 2", mensaje);
        Assert.Contains("PEDIDO N° 42", mensaje);
        Assert.Contains("PEDIDO N° 43", mensaje);
        Assert.Contains("TOTAL GENERAL:", mensaje);
    }

    [Fact]
    public void ConstruirUrl_CodificaMensajeWhatsappCorrectamente()
    {
        var url = PedidoMensajeBuilder.ConstruirUrl("51987654321", "Pedido N° 1\nTotal: S/ 10.00 & más");

        Assert.StartsWith("https://wa.me/51987654321?text=", url);
        Assert.Contains(Uri.EscapeDataString("Pedido N° 1\nTotal: S/ 10.00 & más"), url);
        Assert.DoesNotContain(" ", url.Replace("https://wa.me/51987654321?text=", string.Empty));
    }
}

