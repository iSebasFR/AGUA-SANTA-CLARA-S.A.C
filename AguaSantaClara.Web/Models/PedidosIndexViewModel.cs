using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Models;

public class PedidosIndexViewModel
{
    public List<Pedido> Pedidos { get; set; } = new();
    public List<Local> Locales { get; set; } = new();
    public List<Repartidor> Repartidores { get; set; } = new();
    public List<Repartidor> RepartidoresFiltro { get; set; } = new();
    public List<Cliente> ClientesFiltro { get; set; } = new();
    public Dictionary<long, string> MotivosIncidenciaPorPedido { get; set; } = new();
    public string? EstadoFiltro { get; set; }
    public long? IdClienteFiltro { get; set; }
    public long? IdRepartidorFiltro { get; set; }
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
    public int Stock { get; set; }
}