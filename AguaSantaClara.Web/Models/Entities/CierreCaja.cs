namespace AguaSantaClara.Web.Models.Entities;

public static class EstadosCierreCaja
{
    public const string Cuadrado = "Cuadrado";
    public const string ConDiferencia = "Con diferencia";

    public static readonly string[] Todos = { Cuadrado, ConDiferencia };
}

public class CierreCaja
{
    public long Id { get; set; }
    public long IdVendedora { get; set; }
    public DateOnly Fecha { get; set; }

    // Montos ingresados manualmente por la vendedora
    public decimal EfectivoRecibido { get; set; }
    public decimal YapeRecibido { get; set; }
    public decimal GastosDelDia { get; set; }

    // Montos calculados automáticamente y persistidos
    public decimal EfectivoEsperado { get; set; }
    public decimal YapeEsperado { get; set; }

    // Diferencia = (EfectivoRecibido + YapeRecibido) - (EfectivoEsperado + YapeEsperado)
    public decimal Diferencia { get; set; }
    public string Estado { get; set; } = EstadosCierreCaja.Cuadrado;
    public string? Observacion { get; set; }

    // Dinero que queda en caja: lo recibido menos los gastos (no se persiste)
    public decimal TotalEnCaja => EfectivoRecibido + YapeRecibido - GastosDelDia;

    public bool EstadoRegistro { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }

    public Usuario Vendedora { get; set; } = null!;
}