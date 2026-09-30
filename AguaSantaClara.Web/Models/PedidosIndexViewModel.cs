using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Models;

public class PedidosIndexViewModel
{
    public List<Pedido> Pedidos { get; set; } = new();
    public List<Local> Locales { get; set; } = new();
    public List<Repartidor> Repartidores { get; set; } = new();
}

public class ClienteBusquedaViewModel
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public List<DireccionBusquedaViewModel> Direcciones { get; set; } = new();
}

public class DireccionBusquedaViewModel
{
    public long Id { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? UrlUbicacion { get; set; }
    public bool Principal { get; set; }
}

public class ProductoLocalViewModel
{
    public long IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
}
