namespace AguaSantaClara.Web.Models;

public class PanelIndicadoresViewModel
{
    public decimal VentasDelMes { get; set; }

    public int PedidosDelDia { get; set; }

    public decimal IndiceMorosidad { get; set; }
    public int ProductosStockBajo { get; set; }
}