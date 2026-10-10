namespace AguaSantaClara.Web.Models.Alertas;

/// <summary>Una alerta efímera disponible para el usuario autenticado.</summary>
public sealed record NotificacionResponse(
    string Tipo,
    string EntidadAfectada,
    string Mensaje,
    string Prioridad,
    DateTimeOffset FechaDeteccion,
    int? StockActual,
    int? StockMinimo,
    DateTimeOffset? FechaVencimiento,
    decimal? Monto);
