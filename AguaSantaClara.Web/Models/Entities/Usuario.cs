using Microsoft.AspNetCore.Identity;

namespace AguaSantaClara.Web.Models.Entities;

public class Usuario : IdentityUser<long>
{
    public long IdRol { get; set; }
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Rol Rol { get; set; } = null!;
}