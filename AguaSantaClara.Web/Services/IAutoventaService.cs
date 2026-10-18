using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Services;

// HU-14 T10: registro de autoventas en ruta
public interface IAutoventaService
{
    // Valida, recalcula el total con los precios de la base y guarda todo en una sola transacción.
    // Si hay errores no se guarda nada. Mensaje de éxito:
    //   cliente existente -> "Autoventa registrada correctamente"
    //   cliente nuevo     -> "Autoventa y cliente registrados correctamente"
    Task<PedidoResultado> GuardarAsync(GuardarAutoventaViewModel modelo);
}