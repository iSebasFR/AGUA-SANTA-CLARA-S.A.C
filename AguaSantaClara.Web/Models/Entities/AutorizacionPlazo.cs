namespace AguaSantaClara.Web.Models.Entities;

public class AutorizacionPlazo
{
    public long Id { get; set; }
    public long IdDeuda { get; set; }
    public long IdUsuarioAutoriza { get; set; }

    public decimal MontoAnterior { get; set; }
    public decimal MontoNuevo { get; set; }
    public DateOnly FechaVencimientoAnterior { get; set; }
    public DateOnly FechaVencimientoNueva { get; set; }
    public string? Observacion { get; set; }

    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Deuda Deuda { get; set; } = null!;
    public Usuario UsuarioAutoriza { get; set; } = null!;
}