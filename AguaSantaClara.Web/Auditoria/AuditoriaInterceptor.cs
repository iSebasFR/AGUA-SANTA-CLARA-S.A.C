using System.Security.Claims;
using System.Text.Json;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AguaSantaClara.Web.Auditoria;

/// <summary>
/// Captura inserciones, ediciones y eliminaciones de todas las entidades de EF Core
/// y las registra en <see cref="AuditoriaCambio"/> dentro del mismo SaveChanges.
/// Se registra como scoped: el estado pendiente pertenece a un único DbContext.
/// </summary>
public class AuditoriaInterceptor : SaveChangesInterceptor
{
    // No se auditan la propia tabla de auditoría ni las tablas puente/técnicas de Identity.
    private static readonly HashSet<Type> TiposExcluidos =
    [
        typeof(AuditoriaCambio),
        typeof(IdentityUserRole<long>),
        typeof(IdentityUserClaim<long>),
        typeof(IdentityUserLogin<long>),
        typeof(IdentityUserToken<long>),
        typeof(IdentityRoleClaim<long>),
    ];

    // Datos sensibles o puramente técnicos que nunca deben quedar en el historial.
    private static readonly HashSet<string> PropiedadesExcluidas =
    [
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "FechaActualizacion",
    ];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly List<(EntityEntry Entry, string NuevosValores)> _insertsPendientes = [];

    public AuditoriaInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capturar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capturar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var context = eventData.Context;
        if (context is not null && RegistrarInsertsPendientes(context))
            context.SaveChanges();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null && RegistrarInsertsPendientes(context))
            await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _insertsPendientes.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _insertsPendientes.Clear();
        return Task.CompletedTask;
    }

    private void Capturar(DbContext? context)
    {
        if (context is null) return;

        _insertsPendientes.Clear();
        var (idUsuario, nombreUsuario) = ObtenerUsuarioActual();
        var ahora = DateTime.UtcNow;

        // ToList: agregar auditorías al ChangeTracker modifica su colección durante el recorrido.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (!EsAuditable(entry)) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    // La clave aún es temporal: el registro se completa en SavedChanges.
                    _insertsPendientes.Add((entry, Serializar(
                        Valores(entry, p => p.CurrentValue))));
                    break;

                case EntityState.Modified:
                    var anteriores = new Dictionary<string, object?>();
                    var nuevos = new Dictionary<string, object?>();
                    foreach (var p in entry.Properties.Where(p => p.IsModified
                                 && !PropiedadesExcluidas.Contains(p.Metadata.Name)
                                 && !Equals(p.OriginalValue, p.CurrentValue)))
                    {
                        anteriores[p.Metadata.Name] = p.OriginalValue;
                        nuevos[p.Metadata.Name] = p.CurrentValue;
                    }
                    if (nuevos.Count == 0) break;

                    // Borrado lógico: el sistema "elimina" poniendo EstadoRegistro en false.
                    var esBajaLogica = nuevos.TryGetValue("EstadoRegistro", out var er) && er is false;
                    context.Add(Crear(entry, esBajaLogica ? AccionAuditoria.Eliminar : AccionAuditoria.Editar,
                        ObtenerIdRegistro(entry), Serializar(anteriores), Serializar(nuevos),
                        idUsuario, nombreUsuario, ahora));
                    break;

                case EntityState.Deleted:
                    context.Add(Crear(entry, AccionAuditoria.Eliminar, ObtenerIdRegistro(entry),
                        Serializar(Valores(entry, p => p.OriginalValue)), null,
                        idUsuario, nombreUsuario, ahora));
                    break;
            }
        }
    }

    /// <returns>true si se agregaron auditorías de inserts que requieren un segundo guardado.</returns>
    private bool RegistrarInsertsPendientes(DbContext context)
    {
        if (_insertsPendientes.Count == 0) return false;

        var (idUsuario, nombreUsuario) = ObtenerUsuarioActual();
        var ahora = DateTime.UtcNow;
        foreach (var (entry, nuevos) in _insertsPendientes)
        {
            context.Add(Crear(entry, AccionAuditoria.Insertar, ObtenerIdRegistro(entry),
                null, nuevos, idUsuario, nombreUsuario, ahora));
        }
        _insertsPendientes.Clear();
        return true;
    }

    private static bool EsAuditable(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
        && !TiposExcluidos.Contains(entry.Metadata.ClrType);

    private static Dictionary<string, object?> Valores(
        EntityEntry entry, Func<PropertyEntry, object?> selector) =>
        entry.Properties
            .Where(p => !PropiedadesExcluidas.Contains(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, selector);

    private static AuditoriaCambio Crear(
        EntityEntry entry, string accion, string? idRegistro,
        string? anteriores, string? nuevos, long? idUsuario, string nombreUsuario, DateTime fecha) => new()
    {
        IdUsuario = idUsuario,
        NombreUsuario = nombreUsuario,
        Entidad = entry.Metadata.ClrType.Name,
        IdRegistro = idRegistro,
        Accion = accion,
        Fecha = fecha,
        ValoresAnteriores = anteriores,
        ValoresNuevos = nuevos,
    };

    private static string? ObtenerIdRegistro(EntityEntry entry)
    {
        var clave = entry.Metadata.FindPrimaryKey();
        if (clave is null) return null;
        return string.Join("-", clave.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string Serializar(Dictionary<string, object?> valores) =>
        JsonSerializer.Serialize(valores);

    private (long? Id, string Nombre) ObtenerUsuarioActual()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return (null, "Sistema");

        var nombre = user.Identity.Name ?? "Desconocido";
        return long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? (id, nombre)
            : (null, nombre);
    }
}
