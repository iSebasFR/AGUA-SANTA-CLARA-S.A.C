namespace AguaSantaClara.Web.Models.Entities;

public static class MotivosIncidencia
{
    public const string ClienteAusente = "Cliente ausente";
    public const string DireccionIncorrecta = "Dirección incorrecta";
    public const string ProductoDanado = "Producto dañado";
    public const string VehiculoDescompuesto = "Vehículo descompuesto";
    public const string Otros = "Otros";

    public static readonly string[] Todos =
    {
        ClienteAusente, DireccionIncorrecta, ProductoDanado, VehiculoDescompuesto, Otros
    };
}

public class Incidencia
{
    public long Id { get; set; }
    public long IdPedido { get; set; }
    public string Motivo { get; set; } = MotivosIncidencia.ClienteAusente;
    public string? Detalle { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Pedido Pedido { get; set; } = null!;
}