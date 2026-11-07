using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IHistorialComprasService
{
    // HU-19 T29: desde/hasta son fechas de Perú (opcionales). Sin ellas, se devuelve todo el historial.
    Task<HistorialComprasViewModel> ObtenerAsync(long idCliente, DateTime? desde = null, DateTime? hasta = null);
}
