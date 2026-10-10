using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public class DeudaNotificacionService
{
    private readonly AppDbContext _context;

    public DeudaNotificacionService(AppDbContext context) => _context = context;

    public async Task<NotificacionesDeudasViewModel> ObtenerAsync(DateOnly hoy)
    {
        var inicioDelDia = hoy.ToDateTime(TimeOnly.MinValue);
        var deudasVencidas = await _context.Deudas
            .Where(d => d.EstadoRegistro
                && d.Estado == EstadosDeuda.Pendiente
                && d.FechaVencimiento < inicioDelDia)
            .ToListAsync();

        foreach (var deuda in deudasVencidas)
            deuda.Estado = EstadosDeuda.Vencida;

        if (deudasVencidas.Count > 0)
            await _context.SaveChangesAsync();

        var limiteNotificacion = hoy.AddDays(3).ToDateTime(TimeOnly.MaxValue);
        var notificaciones = await _context.Deudas
            .Where(d => d.EstadoRegistro
                && d.Estado == EstadosDeuda.Pendiente
                && d.FechaVencimiento >= inicioDelDia
                && d.FechaVencimiento <= limiteNotificacion)
            .OrderBy(d => d.FechaVencimiento)
            .ThenBy(d => d.Id)
            .Select(d => new NotificacionDeudaViewModel
            {
                IdCliente = d.IdCliente,
                NombreCliente = d.Cliente.Nombre,
                Monto = d.Monto,
                FechaVencimiento = d.FechaVencimiento
            })
            .ToListAsync();

        return new NotificacionesDeudasViewModel { Notificaciones = notificaciones };
    }
}
