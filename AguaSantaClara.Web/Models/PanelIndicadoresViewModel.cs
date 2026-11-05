namespace AguaSantaClara.Web.Models;
using System.Collections.Generic;

public class PanelIndicadoresViewModel
{
    public decimal VentasDelMes { get; set; }

    public int PedidosDelDia { get; set; }

    public decimal IndiceMorosidad { get; set; }
    public int ProductosStockBajo { get; set; }

    public List<string> MesesVentas { get; set; } = new();

    public List<decimal> VentasUltimosSeisMeses { get; set; } = new();
}