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
}