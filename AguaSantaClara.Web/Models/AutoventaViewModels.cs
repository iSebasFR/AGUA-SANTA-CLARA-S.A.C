namespace AguaSantaClara.Web.Models;

// HU-14 T10: datos del cliente nuevo que se registra junto con la autoventa
public class ClienteNuevoAutoventaViewModel
{
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
    public string? Dni { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Referencia { get; set; }
}

// HU-14 T10: cuerpo de la petición "Guardar autoventa".
// Debe venir IdCliente (cliente existente) o ClienteNuevo, nunca los dos ni ninguno.
// El total y los precios NO viajan: el servidor los calcula con la base de datos.
public class GuardarAutoventaViewModel
{
    public long? IdCliente { get; set; }
    public ClienteNuevoAutoventaViewModel? ClienteNuevo { get; set; }
    public long? IdMetodoPago { get; set; }
    public List<DetallePedidoViewModel> Detalles { get; set; } = new();
}