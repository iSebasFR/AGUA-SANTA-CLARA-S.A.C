namespace AguaSantaClara.Web.Models;

public class ModificarStockProductoViewModel
{
    public long IdProducto { get; set; }

    public long IdLocal { get; set; }

    public string NombreProducto { get; set; } = null!;

    public int StockActual { get; set; }

    public string Accion { get; set; } = "Aumentar";

    public int? Cantidad { get; set; }

    public string? Motivo { get; set; }
}