using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Models;

public sealed record DeudaPedidoResumen(decimal Monto, decimal MontoPagadoAlRegistrar, DateTime FechaVencimiento, string Estado);

public class PedidosIndexViewModel
{
    public List<Pedido> Pedidos { get; set; } = new();
    public List<Local> Locales { get; set; } = new();
    public List<MetodoPago> MetodosPago { get; set; } = new();
    public List<Repartidor> Repartidores { get; set; } = new();
    public List<Repartidor> RepartidoresFiltro { get; set; } = new();
    public List<Cliente> ClientesFiltro { get; set; } = new();
    public Dictionary<long, string> MotivosIncidenciaPorPedido { get; set; } = new();
    public Dictionary<long, Dictionary<long, decimal>> MontosPagadosPorPedidoCliente { get; set; } = new();
    public Dictionary<long, Dictionary<long, DeudaPedidoResumen>> DeudasPorPedidoCliente { get; set; } = new();
    public string? EstadoFiltro { get; set; }
    public long? IdClienteFiltro { get; set; }
    public long? IdRepartidorFiltro { get; set; }
    public string? ErrorFiltros { get; set; }
    public bool TieneFiltrosAplicados { get; set; }

    // HU-14 T13: resumen de ventas de hoy (pedidos normales vs autoventas)
    public int PedidosNormalesHoy { get; set; }
    public decimal MontoPedidosNormalesHoy { get; set; }
    public int AutoventasHoy { get; set; }
    public decimal MontoAutoventasHoy { get; set; }
    public Dictionary<string, decimal> CobradoAutoventasPorMetodoHoy { get; set; } = new();
    
}

public class RegistrarPagosPedidoViewModel
{
    public List<PagoClientePedidoViewModel> Pagos { get; set; } = new();
}

public class RegistrarDeudaViewModel
{
    public long IdCliente { get; set; }
    public decimal Monto { get; set; }
    public DateTime? FechaVencimiento { get; set; }
}

public class AutorizarPlazoViewModel
{
    public long IdCliente { get; set; }
    public decimal MontoNuevo { get; set; }
    public DateTime? FechaVencimientoNueva { get; set; }
    public string? Observacion { get; set; }
}

public class PagoClientePedidoViewModel
{
    public long IdCliente { get; set; }
    public long IdMetodoPago { get; set; }
    public decimal Monto { get; set; }
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