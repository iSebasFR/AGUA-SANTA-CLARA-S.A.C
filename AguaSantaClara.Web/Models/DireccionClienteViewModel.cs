using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class DireccionClienteViewModel
{
    public long Id { get; set; }

    public long IdCliente { get; set; }

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

    [Display(Name = "Dirección principal")]
    public bool Principal { get; set; }
}