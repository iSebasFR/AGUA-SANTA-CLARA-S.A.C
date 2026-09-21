using Microsoft.AspNetCore.Identity;

namespace AguaSantaClara.Web.Models.Entities;

public class Rol : IdentityRole<long>
{
    public string Codigo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}