using AguaSantaClara.Web.Models.Entities;
using AguaSantaClara.Web.Services;

namespace AguaSantaClara.Tests;

public class PedidoMensajeBuilderTests
{
    private static Cliente Cliente(long id, string nombre) => new() { Id = id, Nombre = nombre };

    private static DireccionCliente Direccion(long id, string direccion, string? url = null, string? ciudad = null) =>
        new() { Id = id, Direccion = direccion, UrlUbicacion = url, Ciudad = ciudad };

    private static PedidoCliente Linea(Cliente cliente, DireccionCliente direccion, params (string producto, int cantidad, decimal precio)[] items)
    {
        var linea = new PedidoCliente
        {
            IdCliente = cliente.Id, Cliente = cliente, IdDireccion = direccion.Id, Direccion = direccion
        };
        foreach (var (producto, cantidad, precio) in items)
            linea.Detalles.Add(new DetallePedido
            {
                Producto = new Producto { Nombre = producto }, Cantidad = cantidad,
                PrecioUnitario = precio, Subtotal = cantidad * precio
            });
        linea.Subtotal = linea.Detalles.Sum(d => d.Subtotal);
        return linea;
    }

    private static Pedido Pedido(params PedidoCliente[] lineas)
    {
        var pedido = new Pedido
        {
            Id = 42, Local = new Local { Nombre = "Santa Rosa" },
            FechaEntrega = new DateTime(2026, 10, 5, 14, 30, 0)
        };
        foreach (var linea in lineas) pedido.Clientes.Add(linea);
        pedido.Total = lineas.Sum(l => l.Subtotal);
        return pedido;
    }

    [Fact]
    public void Construir_IncluyeNumeroClienteDireccionUbicacionProductosFechaYTotalAlFinal()
    {
        var ana = Cliente(1, "Ana Torres");
        var mensaje = PedidoMensajeBuilder.Construir(Pedido(
            Linea(ana, Direccion(1, "Av. Lima 123", "https://maps.app.goo.gl/x", "Lima"), ("Galones 11L", 2, 12.5m))));

        Assert.Contains("Pedido N° 42", mensaje);
        Assert.Contains("Entrega: 05/10/2026 14:30", mensaje);
        Assert.Contains("Cliente: Ana Torres", mensaje);
        Assert.Contains("Dirección: Av. Lima 123, Lima", mensaje);
        Assert.Contains("Ubicación: https://maps.app.goo.gl/x", mensaje);
        Assert.Contains("- 2 x Galones 11L = S/ 25.00", mensaje);
        Assert.EndsWith("Total: S/ 25.00", mensaje);
    }

    [Fact]
    public void Construir_ConUnSoloClienteNoMuestraSubtotales()
    {
        var ana = Cliente(1, "Ana Torres");
        var mensaje = PedidoMensajeBuilder.Construir(Pedido(
            Linea(ana, Direccion(1, "Av. Lima 123"), ("Galones 11L", 1, 10m)),
            Linea(ana, Direccion(2, "Jr. Cusco 45"), ("Bidones 20L", 1, 10m))));

        Assert.DoesNotContain("Subtotal", mensaje);
    }

    [Fact]
    public void Construir_AgrupaLasLineasDelMismoClienteBajoUnSoloEncabezado()
    {
        var ana = Cliente(1, "Ana Torres");
        var mensaje = PedidoMensajeBuilder.Construir(Pedido(
            Linea(ana, Direccion(1, "Av. Lima 123"), ("Galones 11L", 1, 10m)),
            Linea(ana, Direccion(2, "Jr. Cusco 45"), ("Bidones 20L", 1, 10m))));

        Assert.Equal(1, CuentaOcurrencias(mensaje, "Cliente: Ana Torres"));
        Assert.Contains("Dirección: Av. Lima 123", mensaje);
        Assert.Contains("Dirección: Jr. Cusco 45", mensaje);
    }

    [Fact]
    public void Construir_ConVariosClientesMuestraSubtotalPorClienteYTotalGeneralAlFinal()
    {
        var ana = Cliente(1, "Ana Torres");
        var beto = Cliente(2, "Beto Ruiz");
        var mensaje = PedidoMensajeBuilder.Construir(Pedido(
            Linea(ana, Direccion(1, "Av. Lima 123"), ("Galones 11L", 1, 10m)),
            Linea(ana, Direccion(2, "Jr. Cusco 45"), ("Bidones 20L", 2, 10m)),
            Linea(beto, Direccion(3, "Calle Sol 9"), ("Bolsas de Hielo", 3, 5m))));

        Assert.Contains("Subtotal Ana Torres: S/ 30.00", mensaje);
        Assert.Contains("Subtotal Beto Ruiz: S/ 15.00", mensaje);
        Assert.EndsWith("Total: S/ 45.00", mensaje);
    }

    [Fact]
    public void Construir_OmiteLaLineaDeUbicacionCuandoLaDireccionNoTieneUrl()
    {
        var mensaje = PedidoMensajeBuilder.Construir(Pedido(
            Linea(Cliente(1, "Ana Torres"), Direccion(1, "Av. Lima 123"), ("Galones 11L", 1, 10m))));

        Assert.DoesNotContain("Ubicación:", mensaje);
    }

    [Fact]
    public void ConstruirUrl_CodificaElTextoConEscapeDataStringYUsaElCelularSinMas()
    {
        var url = PedidoMensajeBuilder.ConstruirUrl("51987654321", "Pedido N° 1\nTotal: S/ 10.00 & más");

        Assert.Equal(
            "https://wa.me/51987654321?text=" + Uri.EscapeDataString("Pedido N° 1\nTotal: S/ 10.00 & más"),
            url);
        Assert.DoesNotContain("+", url.Replace("https://wa.me/", string.Empty));
        Assert.DoesNotContain(" ", url);
        Assert.DoesNotContain("\n", url);
    }

    private static int CuentaOcurrencias(string texto, string buscado)
    {
        var cuenta = 0;
        var indice = 0;
        while ((indice = texto.IndexOf(buscado, indice, StringComparison.Ordinal)) >= 0)
        {
            cuenta++;
            indice += buscado.Length;
        }
        return cuenta;
    }
}
