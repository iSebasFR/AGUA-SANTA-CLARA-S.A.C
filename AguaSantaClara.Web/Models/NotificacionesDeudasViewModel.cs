namespace AguaSantaClara.Web.Models;

public class NotificacionesDeudasViewModel
{
    public List<NotificacionDeudaViewModel> Notificaciones { get; set; } = new();
}

public class NotificacionDeudaViewModel
{
    public long IdCliente { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime FechaVencimiento { get; set; }
}
