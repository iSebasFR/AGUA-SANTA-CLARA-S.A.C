namespace AguaSantaClara.Web.Models;

public class CrearPedidoViewModel
{
    public long IdLocal { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public long? IdRepartidor { get; set; }
    public List<PedidoClienteViewModel> Clientes { get; set; } = new();
}

public class PedidoClienteViewModel
{
    public long IdCliente { get; set; }
    public long IdDireccion { get; set; }
    public List<DetallePedidoViewModel> Detalles { get; set; } = new();
}

public class DetallePedidoViewModel
{
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
}
