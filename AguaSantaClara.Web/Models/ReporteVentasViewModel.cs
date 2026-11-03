namespace AguaSantaClara.Web.Models;

public class ReporteVentasViewModel
{
    public string FiltroRapido { get; set; } = "hoy"; // "hoy", "ayer", "semana", "personalizado"
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }

    public decimal TotalVentas { get; set; }
    public decimal TotalPagos { get; set; }
    public decimal TotalDeudas { get; set; }
    public int CantidadPedidos { get; set; }
    public int CantidadPagos { get; set; }
    public int CantidadDeudas { get; set; }

    public List<PedidoReporteItemViewModel> Pedidos { get; set; } = new();
    public List<PagoReporteItemViewModel> Pagos { get; set; } = new();
    public List<DeudaReporteItemViewModel> Deudas { get; set; } = new();
}

public class PedidoReporteItemViewModel
{
    public long Id { get; set; }
    public string Numero => $"#{Id:D4}";
    public string Cliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class PagoReporteItemViewModel
{
    public long Id { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string MetodoPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class DeudaReporteItemViewModel
{
    public long Id { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal Monto { get; set; }
}
