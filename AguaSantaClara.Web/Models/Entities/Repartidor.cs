namespace AguaSantaClara.Web.Models.Entities;

public class Repartidor
{
    public long Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Celular { get; set; } = null!;
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
}
