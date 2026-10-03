using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AguaSantaClara.Web.Models;

public class ProductosIndexViewModel
{
    public List<Producto> Productos { get; set; } = new();

    // Filtros
    public string? Search { get; set; }
    public string? CategoriaFiltro { get; set; }
    public bool? EstadoFiltro { get; set; }

    // Listas para dropdowns
    public IEnumerable<SelectListItem> Categorias { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "Todas las categorías" },
        new SelectListItem { Value = "Bidón", Text = "Bidón" },
        new SelectListItem { Value = "Galón", Text = "Galón" },
        new SelectListItem { Value = "Hielo", Text = "Hielo" },
        new SelectListItem { Value = "Papel", Text = "Papel Higiénico" }
    };

    public IEnumerable<SelectListItem> Estados { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "Todos los estados" },
        new SelectListItem { Value = "true", Text = "Activos" },
        new SelectListItem { Value = "false", Text = "Inactivos" }
    };
}