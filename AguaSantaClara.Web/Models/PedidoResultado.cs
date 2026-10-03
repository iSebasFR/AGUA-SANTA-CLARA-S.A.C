namespace AguaSantaClara.Web.Models;

public record ErrorPedido(string Campo, string Mensaje);

public class PedidoResultado
{
    public bool Ok => Errores.Count == 0;
    public List<ErrorPedido> Errores { get; set; } = new();
    public long? IdPedido { get; set; }
    public decimal Total { get; set; }
    public string? WhatsappUrl { get; set; }
    public string? Mensaje { get; set; }
}