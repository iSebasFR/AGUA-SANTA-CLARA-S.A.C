namespace AguaSantaClara.Web.Models;

// HU-21: montos sin redondear; el redondeo se hace solo al mostrar.
public class RentabilidadViewModel
{
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public decimal IngresoNeto { get; set; }
    public decimal Costo { get; set; }
    public decimal Margen { get; set; }
    public decimal? PorcentajeMargen { get; set; }
    public List<LineaRentabilidadViewModel> Lineas { get; set; } = new();
    public List<string> CategoriasSinLinea { get; set; } = new();
}

public class LineaRentabilidadViewModel
{
    public string Nombre { get; set; } = string.Empty;
    public decimal IngresoBruto { get; set; }
    public decimal Descuentos { get; set; }
    public decimal IngresoNeto { get; set; }
    public decimal Costo { get; set; }
    public decimal Margen { get; set; }
    public decimal? PorcentajeMargen { get; set; }
    public bool MargenNegativo => Margen < 0;
    public List<ProductoRentabilidadViewModel> Productos { get; set; } = new();
}

public class ProductoRentabilidadViewModel
{
    public long IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal IngresoBruto { get; set; }
    public decimal Descuentos { get; set; }
    public decimal IngresoNeto { get; set; }
    public decimal Costo { get; set; }
    public decimal Margen { get; set; }
    public decimal? PorcentajeMargen { get; set; }
    public bool MargenNegativo => Margen < 0;
}
