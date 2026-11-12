using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

public interface IRentabilidadService
{
    // HU-21: desde/hasta son fechas locales inclusivas, igual que en el Reporte de Ventas.
    Task<RentabilidadViewModel> ObtenerRentabilidadAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}
