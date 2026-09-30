namespace AguaSantaClara.Web.Models.Entities;

public static class EstadosDeuda
{
    public const string Pendiente = "Pendiente";
    public const string Pagada = "Pagada";
    public const string Vencida = "Vencida";

    public static readonly string[] Todos = { Pendiente, Pagada, Vencida };
}

public class Deuda
{
    public long Id { get; set; }
    public long IdPedido { get; set; }
    public long IdCliente { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string Estado { get; set; } = EstadosDeuda.Pendiente;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
}