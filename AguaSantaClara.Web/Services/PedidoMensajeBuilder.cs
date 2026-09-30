using System.Globalization;
using System.Text;
using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Services;

public static class PedidoMensajeBuilder
{
    public static string Construir(Pedido pedido)
    {
        var sb = new StringBuilder();
        var grupos = pedido.Clientes.GroupBy(linea => linea.IdCliente).ToList();
        var variosClientes = grupos.Count > 1;

        Linea(sb, $"Pedido N° {pedido.Id}");
        Linea(sb, $"Entrega: {pedido.FechaEntrega.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
        Linea(sb, $"Local: {pedido.Local.Nombre}");

        foreach (var grupo in grupos)
        {
            Linea(sb, string.Empty);
            Linea(sb, $"Cliente: {grupo.First().Cliente.Nombre}");

            foreach (var linea in grupo)
            {
                var direccion = string.IsNullOrWhiteSpace(linea.Direccion.Ciudad)
                    ? linea.Direccion.Direccion
                    : $"{linea.Direccion.Direccion}, {linea.Direccion.Ciudad}";

                Linea(sb, $"Dirección: {direccion}");
                if (!string.IsNullOrWhiteSpace(linea.Direccion.UrlUbicacion))
                    Linea(sb, $"Ubicación: {linea.Direccion.UrlUbicacion}");

                Linea(sb, "Productos:");
                foreach (var detalle in linea.Detalles)
                    Linea(sb, $"- {detalle.Cantidad} x {detalle.Producto.Nombre} = {Moneda(detalle.Subtotal)}");
            }

            if (variosClientes)
                Linea(sb, $"Subtotal {grupo.First().Cliente.Nombre}: {Moneda(grupo.Sum(linea => linea.Subtotal))}");
        }

        Linea(sb, string.Empty);
        sb.Append($"Total: {Moneda(pedido.Total)}");
        return sb.ToString();
    }

    public static string ConstruirUrl(string celular, string mensaje) =>
        $"https://wa.me/{celular}?text={Uri.EscapeDataString(mensaje)}";

    private static void Linea(StringBuilder sb, string texto) => sb.Append(texto).Append('\n');

    private static string Moneda(decimal monto) =>
        "S/ " + monto.ToString("0.00", CultureInfo.InvariantCulture);
}
