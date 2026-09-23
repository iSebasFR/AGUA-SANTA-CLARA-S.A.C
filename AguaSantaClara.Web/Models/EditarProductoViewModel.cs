using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class EditarProductoViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
    public string? Descripcion { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio de venta debe ser mayor que 0.")]
    public decimal PrecioVenta { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser mayor que 0.")]
    public decimal Costo { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock actual no puede ser negativo.")]
    public int StockActual { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; set; }

    public bool Estado { get; set; }
}