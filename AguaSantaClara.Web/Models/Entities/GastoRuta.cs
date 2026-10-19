namespace AguaSantaClara.Web.Models.Entities;

public static class TiposGastoRuta
{
    public const string Gasolina = "Gasolina";
    public const string Peajes = "Peajes";
    public const string Cochera = "Cochera";
    public const string Otros = "Otros";

    public static readonly string[] Todos = { Gasolina, Peajes, Cochera, Otros };
}

public class GastoRuta
{
    public long Id { get; set; }
    public long IdRepartidor { get; set; }
    public string Tipo { get; set; } = TiposGastoRuta.Otros;
    public decimal Monto { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Observacion { get; set; }
    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Repartidor Repartidor { get; set; } = null!;
}