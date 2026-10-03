using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IPedidoService
{
    Task<PedidoResultado> CrearAsync(CrearPedidoViewModel modelo, bool enviar);
    Task<PedidoResultado> ActualizarAsync(long idPedido, CrearPedidoViewModel modelo);
    Task<bool> EliminarAsync(long idPedido);
    Task<PedidoResultado> EnviarVariosAsync(EnviarPedidosViewModel modelo);
    Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string termino);
    Task<List<ProductoPedidoViewModel>> ProductosDisponiblesAsync();
}
