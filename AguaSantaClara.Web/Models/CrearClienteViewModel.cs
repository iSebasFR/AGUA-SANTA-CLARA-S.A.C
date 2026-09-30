using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class CrearClienteViewModel
{
    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre / Razón social")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20)]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener 8 dígitos.")]
    [Display(Name = "DNI")]
    public string? Dni { get; set; }

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(255)]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Ciudad")]
    public string? Ciudad { get; set; }

    [StringLength(255)]
    [Display(Name = "Referencia")]
    public string? Referencia { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "La URL de ubicación no es válida.")]
    [Display(Name = "URL de ubicación")]
    public string? UrlUbicacion { get; set; }

    [Display(Name = "Estado")]
    public bool Estado { get; set; } = true;
}