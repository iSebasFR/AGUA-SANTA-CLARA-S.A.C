namespace AguaSantaClara.Web.Models.Entities;

public class MovimientoBidones
{
    public long Id { get; set; }
    public long IdCliente { get; set; }
    public int BidonesEntregados { get; set; }
    public int BidonesDevueltos { get; set; }
    public int Saldo { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Cliente Cliente { get; set; } = null!;
}