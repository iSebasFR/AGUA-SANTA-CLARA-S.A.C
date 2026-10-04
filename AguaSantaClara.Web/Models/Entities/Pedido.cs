namespace AguaSantaClara.Web.Models.Entities;

public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";
    public const string Enviado = "Enviado";
    public const string Entregado = "Entregado";

    public static readonly string[] Todos = { Pendiente, Enviado, Entregado };

    /// <summary>
    /// Estados a los que se puede transicionar desde <paramref name="estadoActual"/>.
    /// </summary>
    public static IEnumerable<string> SiguientesPermitidos(string estadoActual) => estadoActual switch
    {
        Pendiente => new[] { Enviado },
        Enviado => new[] { Entregado },
        Entregado => Array.Empty<string>(),
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