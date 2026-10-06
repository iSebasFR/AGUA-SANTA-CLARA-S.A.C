namespace AguaSantaClara.Web.Models.Entities;

public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";
    public const string Enviado = "Enviado";
    public const string Entregado = "Entregado";
    public const string ConIncidencia = "Con Incidencia";
    public const string Pagado = "Pagado";
    public const string PagoParcial = "Pago Parcial";

    public static readonly string[] Todos =
    {
        Pendiente,
        Enviado,
        Entregado,
        ConIncidencia,
        Pagado,
        PagoParcial
    };

    public static IEnumerable<string> SiguientesPermitidos(string estadoActual) => estadoActual switch
    {
        Pendiente => new[] { Enviado },
        Enviado => new[] { Entregado, ConIncidencia, Pagado, PagoParcial },
        Entregado => new[] { Pagado, PagoParcial },
        ConIncidencia => new[] { Pagado, PagoParcial },
        Pagado => Array.Empty<string>(),
        PagoParcial => Array.Empty<string>(),
        _ => Array.Empty<string>()
    };
}

public class Pedido
{
    public long Id { get; set; }
    public long? IdRepartidor { get; set; }
    public string Estado { get; set; } = EstadosPedido.Pendiente;
    public decimal Total { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Repartidor? Repartidor { get; set; }
    public ICollection<PedidoCliente> Clientes { get; set; } = new List<PedidoCliente>();
}