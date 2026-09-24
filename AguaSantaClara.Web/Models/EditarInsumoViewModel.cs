using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class EditarInsumoViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "La descripción no puede superar los 255 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "El costo es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El costo debe ser mayor a 0.")]
    public decimal Costo { get; set; }

    public bool Estado { get; set; } = true;
}