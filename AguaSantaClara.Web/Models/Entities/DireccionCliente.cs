namespace AguaSantaClara.Web.Models.Entities;

public class DireccionCliente
{
    public long Id { get; set; }
    public long IdCliente { get; set; }
    public string Direccion { get; set; } = null!;
    public string? Referencia { get; set; }
    public string? Ciudad { get; set; }
    public bool Principal { get; set; }
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Cliente Cliente { get; set; } = null!;
}