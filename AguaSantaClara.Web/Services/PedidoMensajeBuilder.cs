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
        var creacion = DateTime.SpecifyKind(pedido.FechaCreacion, DateTimeKind.Utc).ToLocalTime();
        Linea(sb, $"Fecha de creación: {creacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}");
        if (pedido.Local != null)
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

    public static string ConstruirVarios(IEnumerable<Pedido> pedidos)
    {
        var lista = pedidos.OrderBy(p => p.Id).ToList();
        var sb = new StringBuilder();

        Linea(sb, $"Pedidos asignados: {lista.Count}");
        foreach (var pedido in lista)
        {
            Linea(sb, string.Empty);
            Linea(sb, "────────────");
            Linea(sb, Construir(pedido));
        }

        Linea(sb, string.Empty);
        Linea(sb, "────────────");
        sb.Append($"Total general: {Moneda(lista.Sum(p => p.Total))}");
        return sb.ToString();
    }

    public static string ConstruirUrl(string celular, string mensaje) =>
        $"https://wa.me/{celular}?text={Uri.EscapeDataString(mensaje)}";

    private static void Linea(StringBuilder sb, string texto) => sb.Append(texto).Append('\n');

    private static string Moneda(decimal monto) =>
        "S/ " + monto.ToString("0.00", CultureInfo.InvariantCulture);
}
