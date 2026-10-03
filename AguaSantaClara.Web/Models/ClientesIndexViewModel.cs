using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AguaSantaClara.Web.Models;

public class ClientesIndexViewModel
{
    // ===== LISTA =====
    public List<Cliente> Clientes { get; set; } = new();

    // ===== FICHA SELECCIONADA (PANEL DERECHO) =====
    public Cliente? Seleccionado { get; set; }

    // ===== HISTORIAL DE PEDIDOS DEL CLIENTE SELECCIONADO =====
    public List<Pedido> Pedidos { get; set; } = new();

    // ===== FILTROS =====
    public string? Search { get; set; }
    public bool? EstadoFiltro { get; set; }

    public IEnumerable<SelectListItem> Estados { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "Todos los estados" },
        new SelectListItem { Value = "true", Text = "Activos" },
        new SelectListItem { Value = "false", Text = "Inactivos" }
    };
}