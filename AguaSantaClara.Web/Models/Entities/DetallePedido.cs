namespace AguaSantaClara.Web.Models.Entities;

public class DetallePedido
{
    public long Id { get; set; }
    public long IdPedidoCliente { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public PedidoCliente PedidoCliente { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
