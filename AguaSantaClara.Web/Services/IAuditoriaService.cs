using AguaSantaClara.Web.Models.Entities;

namespace AguaSantaClara.Web.Services;

public record FiltroAuditoria(
    string? Entidad = null,
    string? Accion = null,
    string? Usuario = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null);

public record ResultadoAuditoria(
    IReadOnlyList<AuditoriaCambio> Items, int Pagina, int TamanoPagina, int Total)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)TamanoPagina);
}

public interface IAuditoriaService
{
    Task<ResultadoAuditoria> ConsultarAsync(FiltroAuditoria filtro, int pagina = 1, int tamanoPagina = 20);
    Task<AuditoriaCambio?> ObtenerAsync(long id);
    Task<IReadOnlyList<string>> ObtenerEntidadesAsync();
}
