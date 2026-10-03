using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Models;

public class ClientesIndexViewModel
{
    public List<Cliente> Clientes { get; set; } = new();
    public Cliente? Seleccionado { get; set; }
    public string? Search { get; set; }
    public List<Pedido> Pedidos { get; set; } = new();   // 👈 Lista directa de entidades
}