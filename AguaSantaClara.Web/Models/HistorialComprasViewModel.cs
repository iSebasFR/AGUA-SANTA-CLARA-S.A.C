namespace AguaSantaClara.Web.Models;

public sealed class HistorialComprasViewModel
{
    public decimal TotalGastado { get; set; }
    public int CantidadPedidos { get; set; }
    public DateTime? FechaUltimaCompra { get; set; }
    public bool EsClienteFrecuente { get; set; }
    public List<PedidoCompraViewModel> Pedidos { get; set; } = [];
}

public sealed class PedidoCompraViewModel
{
    public long IdPedido { get; set; }
    public DateTime Fecha { get; set; }   // ya en hora de Perú (UTC-5)
    public decimal Monto { get; set; }
    public string Estado { get; set; } = string.Empty;
    public List<ProductoCompraViewModel> Productos { get; set; } = [];   // HU-19 T27
}

// HU-19 T27: producto comprado dentro de un pedido
public sealed class ProductoCompraViewModel
{
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal DescuentoMonto { get; set; }
    public decimal Subtotal { get; set; }   // Cantidad * PrecioUnitario - DescuentoMonto
}