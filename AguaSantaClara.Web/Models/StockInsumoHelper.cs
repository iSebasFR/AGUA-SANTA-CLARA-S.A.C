namespace AguaSantaClara.Web.Models;

public static class StockInsumoHelper
{
    // Color de la etiqueta de stock: rojo si es 0, amarillo si está bajo el mínimo, verde en otro caso.
    public static string ClaseCss(int stockActual, int stockMinimo) =>
        stockActual <= 0 ? "stock-agotado"
        : (stockMinimo > 0 && stockActual < stockMinimo ? "stock-bajo" : "stock-ok");
}