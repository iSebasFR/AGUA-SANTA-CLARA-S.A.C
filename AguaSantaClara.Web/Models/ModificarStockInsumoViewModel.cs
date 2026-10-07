namespace AguaSantaClara.Web.Models;

public class ModificarStockInsumoViewModel
{
    public long Id { get; set; }
    public string NombreInsumo { get; set; } = string.Empty;
    public int StockActual { get; set; }

    public string Accion { get; set; } = "Aumentar";
    public int? Cantidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Motivos permitidos según la acción (HU-09).</summary>
public static class MotivosStockInsumo
{
    public static readonly IReadOnlyDictionary<string, string[]> PorAccion =
        new Dictionary<string, string[]>
        {
            ["Aumentar"] = new[] { "Compra", "Ajuste" },
            ["Descontar"] = new[] { "Producción", "Merma/Pérdida", "Ajuste" }
        };
}