using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AguaSantaClara.Web.Models;

public class EditarUsuarioViewModel
{
    public long Id { get; set; }

    [Display(Name = "Usuario")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [Display(Name = "Nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [Display(Name = "Apellidos")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione un rol.")]
    [Display(Name = "Rol")]
    public long? IdRol { get; set; }

    public bool Estado { get; set; }
    public string NombreRol { get; set; } = string.Empty;
    public IEnumerable<string> Permisos { get; set; } = [];
    public IEnumerable<SelectListItem> Roles { get; set; } = [];
}