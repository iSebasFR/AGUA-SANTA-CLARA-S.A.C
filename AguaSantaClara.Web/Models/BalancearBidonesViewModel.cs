namespace AguaSantaClara.Web.Models;

public class BalancearBidonesViewModel
{
    public long IdProducto { get; set; }
    public long IdLocal { get; set; }
    public string NombreProducto { get; set; } = null!;
    public int StockActual { get; set; }

    public string TipoMovimiento { get; set; } = "Entrega";

    public long IdCliente { get; set; }
    public string? NombreCliente { get; set; }
    public string? TelefonoCliente { get; set; }
    public string? DniCliente { get; set; }
    public int SaldoClienteActual { get; set; }

    // Modo Entrega
    public int? BidonesLlenosEntregados { get; set; }
    public int? BidonesVaciosDevueltos { get; set; }

    // Modo Producción (Aumentar / Descontar)
    public string AccionProduccion { get; set; } = "Aumentar";
    public int? CantidadProduccion { get; set; }

    public string Motivo { get; set; } = "Entrega a cliente";
    public string? Observaciones { get; set; }
}