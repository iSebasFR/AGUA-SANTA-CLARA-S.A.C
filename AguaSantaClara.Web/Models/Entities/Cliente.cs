namespace AguaSantaClara.Web.Models.Entities;

public class Cliente
{
    public long Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Telefono { get; set; } = null!;
    public string? Email { get; set; }
    public bool Estado { get; set; } = true;
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public ICollection<DireccionCliente> Direcciones { get; set; } = new List<DireccionCliente>();
}