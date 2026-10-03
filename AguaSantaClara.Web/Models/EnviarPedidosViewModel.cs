namespace AguaSantaClara.Web.Models;

public class EnviarPedidosViewModel
{
    public List<long> IdsPedidos { get; set; } = new();
    public long? IdRepartidor { get; set; }
}
