using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IPedidoService
{
    Task<PedidoResultado> CrearAsync(CrearPedidoViewModel modelo, bool enviar);
    Task<PedidoResultado> PreviewCrearAsync(CrearPedidoViewModel modelo);
    Task<PedidoResultado> ActualizarAsync(long idPedido, CrearPedidoViewModel modelo);
    Task<bool> EliminarAsync(long idPedido);
    Task<PedidoResultado> CambiarEstadoAsync(long idPedido, string? estado);
    Task<PedidoResultado> RegistrarIncidenciaAsync(long idPedido,RegistrarIncidenciaViewModel modelo);
    Task<PedidoResultado> EnviarVariosAsync(EnviarPedidosViewModel modelo);
    Task<PedidoResultado> PreviewEnviarVariosAsync(EnviarPedidosViewModel modelo);
    Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string tipo, string termino);
    Task<List<ProductoPedidoViewModel>> ProductosPorLocalAsync(long idLocal);
}