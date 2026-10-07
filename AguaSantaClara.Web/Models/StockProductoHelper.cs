namespace AguaSantaClara.Web.Models;

public static class StockProductoHelper
{
    public static readonly Dictionary<string, List<string>> PorAccion = new()
    {
        ["Aumentar"] = new()
        {
            "Producción",
            "Devolución (de cliente)",
            "Ajuste"
        },

        ["Descontar"] = new()
        {
            "Producto dañado/Merma",
            "Pérdida",
            "Ajuste"
        }
    };
}