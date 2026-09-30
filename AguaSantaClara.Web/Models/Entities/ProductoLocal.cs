namespace AguaSantaClara.Web.Models.Entities;

public class ProductoLocal
{
    public long Id { get; set; }
    public long IdLocal { get; set; }
    public long IdProducto { get; set; }
    public int Stock { get; set; }
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Local Local { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
