using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IPedidoService
{
    Task<PedidoResultado> CrearAsync(CrearPedidoViewModel modelo, bool enviar);
    Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string termino);
    Task<List<ProductoLocalViewModel>> ProductosPorLocalAsync(long idLocal);
}
