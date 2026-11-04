namespace AguaSantaClara.Web.Models;

public sealed class HistorialFinancieroViewModel
{
    public decimal DeudaTotal { get; set; }
    public decimal SaldoAFavor { get; set; }
    public int CantidadDeudasActivas { get; set; }
    public bool TieneDeudasVencidas { get; set; }
    public List<MovimientoFinancieroViewModel> Movimientos { get; set; } = [];
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? ErrorFiltro { get; set; }
}

public sealed class MovimientoFinancieroViewModel
{
    public long IdPedido { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public string? Estado { get; set; }
    public string? MetodoPago { get; set; }
}
