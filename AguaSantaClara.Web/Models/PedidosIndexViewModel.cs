using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Models;

public class PedidosIndexViewModel
{
    public List<Pedido> Pedidos { get; set; } = new();
    public List<Repartidor> Repartidores { get; set; } = new();
    public List<Repartidor> RepartidoresFiltro { get; set; } = new();
    public List<Cliente> ClientesFiltro { get; set; } = new();
    public List<Producto> ProductosFiltro { get; set; } = new();
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? EstadoFiltro { get; set; }
    public long? IdClienteFiltro { get; set; }
    public long? IdRepartidorFiltro { get; set; }
    public long? IdProductoFiltro { get; set; }
    public string? ErrorFiltros { get; set; }
    public bool TieneFiltrosAplicados { get; set; }
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

public class ProductoPedidoViewModel
{
    public long IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}
