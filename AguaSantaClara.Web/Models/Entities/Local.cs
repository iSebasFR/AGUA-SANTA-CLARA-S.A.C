namespace AguaSantaClara.Web.Models.Entities;

public class Local
{
    public long Id { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<ProductoLocal> Productos { get; set; } = new List<ProductoLocal>();
}
