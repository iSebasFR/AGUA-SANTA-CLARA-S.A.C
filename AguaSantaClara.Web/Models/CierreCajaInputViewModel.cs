using System.ComponentModel.DataAnnotations;

namespace AguaSantaClara.Web.Models;

public class CierreCajaInputViewModel
{
    public DateOnly Fecha { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal EfectivoRecibido { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal YapeRecibido { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99")]
    public decimal GastosDelDia { get; set; }

    [StringLength(255)]
    public string? Observacion { get; set; }
}
