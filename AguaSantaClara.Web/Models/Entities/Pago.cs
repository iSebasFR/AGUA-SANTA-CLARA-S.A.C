namespace AguaSantaClara.Web.Models.Entities;

public class Pago
{
    public long Id { get; set; }
    public long IdPedido { get; set; }
    public long IdCliente { get; set; }
    public long IdMetodoPago { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public MetodoPago MetodoPago { get; set; } = null!;
}