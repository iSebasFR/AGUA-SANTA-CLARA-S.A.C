using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AguaSantaClara.Web.Models;

public class EditarProductoViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Seleccione una categoría.")]
    public string Categoria { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "Seleccione al menos un local.")]
    public List<long> IdLocales { get; set; } = new();

    public List<SelectListItem> Locales { get; set; } = new();

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio de venta debe ser mayor que 0.")]
    public decimal PrecioVenta { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser mayor que 0.")]
    public decimal Costo { get; set; }
}