namespace AguaSantaClara.Web.Models;

public sealed class HistorialComprasViewModel
{
    public decimal TotalGastado { get; set; }
    public int CantidadPedidos { get; set; }
    public DateTime? FechaUltimaCompra { get; set; }
    public bool EsClienteFrecuente { get; set; }
}