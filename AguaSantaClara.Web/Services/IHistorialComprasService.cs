using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IHistorialComprasService
{
    Task<HistorialComprasViewModel> ObtenerAsync(long idCliente);
}