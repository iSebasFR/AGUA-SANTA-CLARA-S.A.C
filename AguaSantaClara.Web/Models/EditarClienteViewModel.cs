using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class EditarClienteViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(150)]
    [Display(Name = "Nombre / Razón social")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20)]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(255)]
    [Display(Name = "Dirección principal")]
    public string Direccion { get; set; } = string.Empty;

    [Display(Name = "Estado")]
    public bool Estado { get; set; }
}