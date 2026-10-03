namespace AguaSantaClara.Web.Models.Entities;

public class Insumo
{
    public long Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal Costo { get; set; }
    public string LineaProducto { get; set; } = null!;
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
}