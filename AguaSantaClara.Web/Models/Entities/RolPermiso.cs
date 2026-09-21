namespace AguaSantaClara.Web.Models.Entities;

public class RolPermiso
{
    public long IdRol { get; set; }
    public long IdPermiso { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}