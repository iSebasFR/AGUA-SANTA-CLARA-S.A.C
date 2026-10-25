using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly AppDbContext _context;

    public AuditoriaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResultadoAuditoria> ConsultarAsync(FiltroAuditoria filtro, int pagina = 1, int tamanoPagina = 20)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var consulta = _context.AuditoriasCambio.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Entidad))
            consulta = consulta.Where(a => a.Entidad == filtro.Entidad);

        if (!string.IsNullOrWhiteSpace(filtro.Accion))
            consulta = consulta.Where(a => a.Accion == filtro.Accion);

        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            var usuario = filtro.Usuario.Trim().ToLower();
            consulta = consulta.Where(a => a.NombreUsuario.ToLower().Contains(usuario));
        }

        if (filtro.Desde.HasValue)
        {
            var desde = filtro.Desde.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(a => a.Fecha >= desde);
        }

        if (filtro.Hasta.HasValue)
        {
            var hasta = filtro.Hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(a => a.Fecha < hasta);
        }

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync();

        return new ResultadoAuditoria(items, pagina, tamanoPagina, total);
    }

    public Task<AuditoriaCambio?> ObtenerAsync(long id) =>
        _context.AuditoriasCambio.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IReadOnlyList<string>> ObtenerEntidadesAsync() =>
        await _context.AuditoriasCambio.AsNoTracking()
            .Select(a => a.Entidad).Distinct().OrderBy(e => e).ToListAsync();
}
