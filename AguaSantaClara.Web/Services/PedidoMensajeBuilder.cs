using System.Globalization;
using System.Text;
using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Services;

public static class PedidoMensajeBuilder
{
    private const string SepFuerte = "════════════════════";
    private const string SepSuave = "────────────────────";

    public static string Construir(Pedido pedido)
    {
        var sb = new StringBuilder();
        var grupos = pedido.Clientes
            .GroupBy(linea => linea.IdCliente)
            .OrderBy(g => g.First().Cliente.Nombre)
            .ToList();

        // ============ CABECERA ============
        Linea(sb, $"*PEDIDO N° {pedido.Id}*");
        Linea(sb, SepFuerte);

        var creacion = DateTime.SpecifyKind(pedido.FechaCreacion, DateTimeKind.Utc).ToLocalTime();
        Linea(sb, $"» Fecha: {creacion:dd/MM/yyyy HH:mm}");

        if (pedido.Repartidor != null)
            Linea(sb, $"» Repartidor: {pedido.Repartidor.Nombre}");

        // ============ CLIENTES ============
        foreach (var grupo in grupos)
        {
            var primer = grupo.First();
            Linea(sb, string.Empty);
            Linea(sb, SepSuave);
            Linea(sb, $"*CLIENTE: {primer.Cliente.Nombre}*");
            Linea(sb, $"» Tel: {primer.Cliente.Telefono}");

            if (!string.IsNullOrWhiteSpace(primer.Cliente.Dni))
                Linea(sb, $"» DNI: {primer.Cliente.Dni}");

            var variasDirecciones = grupo.Count() > 1;
            var idxDir = 1;

            foreach (var linea in grupo.OrderBy(l => l.Id))
            {
                var etiqueta = variasDirecciones ? $"*Dirección {idxDir}:*" : "*Dirección:*";
                var textoDir = string.IsNullOrWhiteSpace(linea.Direccion.Ciudad)
                    ? linea.Direccion.Direccion
                    : $"{linea.Direccion.Direccion}, {linea.Direccion.Ciudad}";

                Linea(sb, string.Empty);
                Linea(sb, $"{etiqueta} {textoDir}");

                if (!string.IsNullOrWhiteSpace(linea.Direccion.UrlUbicacion))
                    Linea(sb, $"» Ubicación: {linea.Direccion.UrlUbicacion}");

                Linea(sb, "*Productos:*");

                foreach (var detalle in linea.Detalles)
                {
                    var descripcion = $"{detalle.Cantidad} x {detalle.Producto.Nombre}";

                    if (detalle.DescuentoMonto > 0)
                        descripcion += $" (-S/ {detalle.DescuentoMonto:0.00})";

                    var puntos = Math.Max(1, 30 - descripcion.Length);
                    Linea(sb, $"  • {descripcion} {new string('.', puntos)} {Moneda(detalle.Subtotal)}");
                }

                Linea(sb, $"  *Subtotal: {Moneda(linea.Subtotal)}*");
                idxDir++;
            }

            if (grupos.Count > 1)
            {
                Linea(sb, string.Empty);
                Linea(sb, $"*Subtotal cliente: {Moneda(grupo.Sum(l => l.Subtotal))}*");
            }
        }

        // ============ TOTAL ============
        Linea(sb, string.Empty);
        Linea(sb, SepFuerte);
        Linea(sb, $"*TOTAL: {Moneda(pedido.Total)}*");
        return sb.ToString();
    }

    public static string ConstruirVarios(IEnumerable<Pedido> pedidos)
    {
        var lista = pedidos.OrderBy(p => p.Id).ToList();
        var sb = new StringBuilder();

        Linea(sb, $"*PEDIDOS ASIGNADOS: {lista.Count}*");
        Linea(sb, SepFuerte);

        foreach (var pedido in lista)
        {
            Linea(sb, string.Empty);
            Linea(sb, Construir(pedido));
        }

        Linea(sb, string.Empty);
        Linea(sb, SepFuerte);
        Linea(sb, $"*TOTAL GENERAL: {Moneda(lista.Sum(p => p.Total))}*");
        return sb.ToString();
    }

    public static string ConstruirUrl(string celular, string mensaje) =>
        $"https://wa.me/{celular}?text={Uri.EscapeDataString(mensaje)}";

    private static void Linea(StringBuilder sb, string texto) => sb.Append(texto).Append('\n');

    private static string Moneda(decimal monto) =>
        "S/ " + monto.ToString("0.00", CultureInfo.InvariantCulture);
}