using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AguaSantaClara.Web.Models;

public class UsuariosIndexViewModel
{
    // Listado de usuarios filtrados
    public List<Usuario> Usuarios { get; set; } = new();

    // Filtros
    public string? Search { get; set; }
    public long? IdRolFiltro { get; set; }
    public bool? EstadoFiltro { get; set; }

    // Listas para los dropdowns de filtro
    public IEnumerable<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Estados { get; set; } = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = "Todos los estados" },
        new SelectListItem { Value = "true", Text = "Activos" },
        new SelectListItem { Value = "false", Text = "Inactivos" }
    };
}