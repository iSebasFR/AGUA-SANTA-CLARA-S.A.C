namespace AguaSantaClara.Web.Models.Entities;

public class PedidoCliente
{
    public long Id { get; set; }
    public long IdPedido { get; set; }
    public long IdCliente { get; set; }
    public long IdDireccion { get; set; }
    public decimal Subtotal { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Cliente Cliente { get; set; } = null!;
    public DireccionCliente Direccion { get; set; } = null!;
    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}
