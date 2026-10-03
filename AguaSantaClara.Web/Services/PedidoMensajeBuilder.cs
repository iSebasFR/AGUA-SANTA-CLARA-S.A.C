using System.Globalization;
using System.Text;
using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Services;

public static class PedidoMensajeBuilder
{
    private const string Separador = "════════════════════";

    public static string Construir(Pedido pedido)
    {
        var sb = new StringBuilder();
        var grupos = pedido.Clientes.GroupBy(linea => linea.IdCliente).ToList();
        var variosClientes = grupos.Count > 1;

        Linea(sb, pedido.Id > 0 ? $"*PEDIDO N° {pedido.Id}*" : "*PEDIDO NUEVO*");
        Linea(sb, Separador);

        if (pedido.Local != null)
            Linea(sb, $"» Local: {pedido.Local.Nombre}");

        var creacion = DateTime.SpecifyKind(pedido.FechaCreacion, DateTimeKind.Utc).ToLocalTime();
        Linea(sb, $"» Fecha: {creacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");

        foreach (var grupo in grupos)
        {
            Linea(sb, string.Empty);
            Linea(sb, $"» Cliente: {grupo.First().Cliente.Nombre}");
            Linea(sb, $"» Tel: {grupo.First().Cliente.Telefono}");
            if (!string.IsNullOrWhiteSpace(grupo.First().Cliente.Dni))
                Linea(sb, $"» DNI: {grupo.First().Cliente.Dni}");

            foreach (var linea in grupo)
            {
                var direccion = string.IsNullOrWhiteSpace(linea.Direccion.Ciudad)
                    ? linea.Direccion.Direccion
                    : $"{linea.Direccion.Direccion}, {linea.Direccion.Ciudad}";

                Linea(sb, $"» Dirección: {direccion}");
                if (!string.IsNullOrWhiteSpace(linea.Direccion.UrlUbicacion))
                    Linea(sb, $"» Ubicación: {linea.Direccion.UrlUbicacion}");

                Linea(sb, string.Empty);
                Linea(sb, "*Productos:*");
                foreach (var detalle in linea.Detalles)
                {
                    var lineaProducto = $"  • {detalle.Cantidad} x {detalle.Producto.Nombre}";
                    var puntos = Math.Max(1, 30 - lineaProducto.Length);
                    Linea(sb, $"{lineaProducto} {new string('.', puntos)} {Moneda(detalle.Subtotal)}");
                }
            }

            if (variosClientes)
            {
                Linea(sb, string.Empty);
                Linea(sb, $"*Subtotal {grupo.First().Cliente.Nombre}:* {Moneda(grupo.Sum(l => l.Subtotal))}");
            }
        }

        Linea(sb, string.Empty);
        Linea(sb, Separador);
        sb.Append($"*TOTAL: {Moneda(pedido.Total)}*");
        return sb.ToString();
    }

    public static string ConstruirVarios(IEnumerable<Pedido> pedidos)
    {
        var lista = pedidos.OrderBy(p => p.Id).ToList();
        var sb = new StringBuilder();

        Linea(sb, $"*PEDIDOS ASIGNADOS: {lista.Count}*");
        Linea(sb, Separador);

        foreach (var pedido in lista)
        {
            Linea(sb, string.Empty);
            Linea(sb, Construir(pedido));
        }

        Linea(sb, string.Empty);
        Linea(sb, Separador);
        sb.Append($"*TOTAL GENERAL: {Moneda(lista.Sum(p => p.Total))}*");
        return sb.ToString();
    }

    public static string ConstruirUrl(string celular, string mensaje) =>
        $"https://wa.me/{celular}?text={Uri.EscapeDataString(mensaje)}";

    private static void Linea(StringBuilder sb, string texto) => sb.Append(texto).Append('\n');

    private static string Moneda(decimal monto) =>
        "S/ " + monto.ToString("0.00", CultureInfo.InvariantCulture);
}