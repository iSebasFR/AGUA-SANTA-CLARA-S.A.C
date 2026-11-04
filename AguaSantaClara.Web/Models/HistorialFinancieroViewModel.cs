namespace AguaSantaClara.Web.Models;

public sealed class HistorialFinancieroViewModel
{
    public decimal DeudaTotal { get; set; }
    public decimal SaldoAFavor { get; set; }
    public int CantidadDeudasActivas { get; set; }
    public List<DeudaHistorialViewModel> Deudas { get; set; } = [];
    public List<PagoHistorialViewModel> Pagos { get; set; } = [];
}

public sealed class DeudaHistorialViewModel
{
    public long IdPedido { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public sealed class PagoHistorialViewModel
{
    public long IdPedido { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Monto { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
}
