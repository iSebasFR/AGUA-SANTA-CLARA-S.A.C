using System.Globalization;
using System.Security.Claims;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora")]
public class CierreCajaController : Controller
{
    private readonly AppDbContext _context;

    public CierreCajaController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierres = await _context.CierresCaja
            .AsNoTracking()
            .Where(cierre => cierre.IdVendedora == idVendedora && cierre.EstadoRegistro)
            .OrderByDescending(cierre => cierre.Fecha)
            .ToListAsync();

        return View(cierres);
    }

    [HttpGet]
    public async Task<IActionResult> Resumen(DateOnly fecha)
    {
        if (!ModelState.IsValid || fecha == default)
            return BadRequest(new { message = "Selecciona una fecha válida." });

        var montosEsperados = await CalcularMontosEsperadosAsync(fecha);
        return Json(new
        {
            efectivoEsperado = montosEsperados.Efectivo,
            yapeEsperado = montosEsperados.Yape,
            montoEsperado = montosEsperados.Efectivo + montosEsperados.Yape
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CierreCajaInputViewModel modelo)
    {
        if (modelo.Fecha == default)
            ModelState.AddModelError(nameof(modelo.Fecha), "La fecha es obligatoria.");

        if (decimal.Round(modelo.EfectivoRecibido, 2) != modelo.EfectivoRecibido
            || decimal.Round(modelo.YapeRecibido, 2) != modelo.YapeRecibido
            || decimal.Round(modelo.GastosDelDia, 2) != modelo.GastosDelDia)
        {
            ModelState.AddModelError(string.Empty, "Los montos pueden tener como máximo dos decimales.");
        }

        if (!ModelState.IsValid)
        {
            var errores = ModelState
                .Where(campo => campo.Value?.Errors.Count > 0)
                .SelectMany(campo => campo.Value!.Errors.Select(error => error.ErrorMessage))
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                .Distinct()
                .ToArray();

            return BadRequest(new
            {
                success = false,
                message = errores.Length > 0
                    ? string.Join(" ", errores)
                    : "Revisa los datos ingresados."
            });
        }

        var idVendedoraTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idVendedoraTexto, NumberStyles.None, CultureInfo.InvariantCulture, out var idVendedora))
            return Unauthorized(new { success = false, message = "No se pudo identificar a la vendedora." });

        var vendedoraExiste = await _context.Usuarios
            .AnyAsync(usuario => usuario.Id == idVendedora && usuario.EstadoRegistro);
        if (!vendedoraExiste)
            return Unauthorized(new { success = false, message = "La cuenta de la vendedora ya no está activa." });

        var cierreExistente = await _context.CierresCaja
            .AnyAsync(cierre => cierre.IdVendedora == idVendedora
                && cierre.Fecha == modelo.Fecha
                && cierre.EstadoRegistro);
        if (cierreExistente)
            return Conflict(new { success = false, message = "Ya existe un cierre registrado para esta fecha." });

        var montosEsperados = await CalcularMontosEsperadosAsync(modelo.Fecha);
        var diferencia = modelo.EfectivoRecibido + modelo.YapeRecibido
            - montosEsperados.Efectivo - montosEsperados.Yape;

        var cierre = new CierreCaja
        {
            IdVendedora = idVendedora,
            Fecha = modelo.Fecha,
            EfectivoRecibido = modelo.EfectivoRecibido,
            YapeRecibido = modelo.YapeRecibido,
            GastosDelDia = modelo.GastosDelDia,
            EfectivoEsperado = montosEsperados.Efectivo,
            YapeEsperado = montosEsperados.Yape,
            Diferencia = diferencia,
            Estado = diferencia == 0m
                ? EstadosCierreCaja.Cuadrado
                : EstadosCierreCaja.ConDiferencia,
            Observacion = string.IsNullOrWhiteSpace(modelo.Observacion)
                ? null
                : modelo.Observacion.Trim(),
            EstadoRegistro = true
        };

        _context.CierresCaja.Add(cierre);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "cierre de caja registrado correctamente",
            id = cierre.Id,
            fecha = cierre.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            efectivoEsperado = cierre.EfectivoEsperado,
            yapeEsperado = cierre.YapeEsperado,
            efectivoRecibido = cierre.EfectivoRecibido,
            yapeRecibido = cierre.YapeRecibido,
            diferencia = cierre.Diferencia,
            estado = cierre.Estado
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .Include(registro => registro.Vendedora)
            .FirstOrDefaultAsync(registro => registro.Id == id
                && registro.IdVendedora == idVendedora
                && registro.EstadoRegistro);

        return cierre == null ? NotFound() : View(cierre);
    }

    private async Task<(decimal Efectivo, decimal Yape)> CalcularMontosEsperadosAsync(DateOnly fecha)
    {
        var inicioUtc = InicioDelDiaUtc(fecha);
        var finUtc = InicioDelDiaUtc(fecha.AddDays(1));

        var montosPorMetodo = await _context.Pagos
            .AsNoTracking()
            .Where(pago => pago.EstadoRegistro
                && pago.Pedido.EstadoRegistro
                && pago.FechaPago >= inicioUtc
                && pago.FechaPago < finUtc
                && (pago.MetodoPago.Nombre == "Efectivo" || pago.MetodoPago.Nombre == "Yape"))
            .GroupBy(pago => pago.MetodoPago.Nombre)
            .Select(grupo => new { Metodo = grupo.Key, Monto = grupo.Sum(pago => pago.Monto) })
            .ToListAsync();

        var efectivo = montosPorMetodo
            .Where(monto => string.Equals(monto.Metodo, "Efectivo", StringComparison.OrdinalIgnoreCase))
            .Sum(monto => monto.Monto);
        var yape = montosPorMetodo
            .Where(monto => string.Equals(monto.Metodo, "Yape", StringComparison.OrdinalIgnoreCase))
            .Sum(monto => monto.Monto);

        return (efectivo, yape);
    }

    private static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

    private bool ObtenerIdVendedora(out long idVendedora)
    {
        var idVendedoraTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(idVendedoraTexto, NumberStyles.None, CultureInfo.InvariantCulture, out idVendedora);
    }
}
