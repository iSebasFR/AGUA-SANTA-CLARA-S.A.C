namespace AguaSantaClara.Web.Models.Entities;

public class AuditoriaCambio
{
    public long Id { get; set; }
    public long? IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = "Sistema";
    public string Entidad { get; set; } = null!;
    public string? IdRegistro { get; set; }
    public string Accion { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? ValoresAnteriores { get; set; }
    public string? ValoresNuevos { get; set; }
}

public static class AccionAuditoria
{
    public const string Insertar = "Insertar";
    public const string Editar = "Editar";
    public const string Eliminar = "Eliminar";

    public static readonly string[] Todas = [Insertar, Editar, Eliminar];
}
