
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora,Gerente,Administradora")]
public class GastosController : Controller
{
    private readonly AppDbContext _context;

    public GastosController(AppDbContext context)
    {
        _context = context;
    }

    // Listar gastos y filtrar por repartidor o fecha.
    public async Task<IActionResult> Index(
        long? idRepartidor,
        DateOnly? fecha,
        string? tipo)
    {
        var consulta = _context.GastosRuta
            .AsNoTracking()
            .Include(g => g.Repartidor)
            .Where(g => g.EstadoRegistro)
            .AsQueryable();

        if (idRepartidor.HasValue)
        {
            consulta = consulta.Where(
                g => g.IdRepartidor == idRepartidor.Value);
        }

        if (fecha.HasValue)
        {
            consulta = consulta.Where(
                g => g.Fecha == fecha.Value);
        }

        if (!string.IsNullOrWhiteSpace(tipo) &&
        TiposGastoRuta.Todos.Contains(tipo))
        {
            consulta = consulta.Where(g => g.Tipo == tipo);
        }

        ViewBag.Tipo = tipo;

        ViewBag.Repartidores = await _context.Repartidores
            .AsNoTracking()
            .Where(r => r.Estado && r.EstadoRegistro)
            .OrderBy(r => r.Nombre)
            .ToListAsync();

        ViewBag.IdRepartidor = idRepartidor;
        ViewBag.Fecha = fecha?.ToString("yyyy-MM-dd");

        var gastos = await consulta
            .OrderByDescending(g => g.Fecha)
            .ThenByDescending(g => g.Id)
            .ToListAsync();

        ViewBag.TotalGastos = gastos.Sum(g => g.Monto);

        return View(gastos);
    }

    // Mostrar el formulario para registrar un gasto.
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await CargarRepartidoresSelectAsync();

        var gasto = new GastoRuta
        {
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Tipo = TiposGastoRuta.Gasolina
        };

        return PartialView("_Create", gasto);
    }

    // Guardar un nuevo gasto.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GastoRuta gasto)
    {
        // Validar que el tipo corresponda a los permitidos.
        if (!TiposGastoRuta.Todos.Contains(gasto.Tipo))
        {
            ModelState.AddModelError(
                nameof(gasto.Tipo),
                "Selecciona un tipo de gasto válido.");
        }

        // Validar el monto.
        if (gasto.Monto <= 0)
        {
            ModelState.AddModelError(
                nameof(gasto.Monto),
                "El monto debe ser mayor que cero.");
        }

        // Validar que se haya indicado una fecha.
        if (gasto.Fecha == default)
        {
            ModelState.AddModelError(
                nameof(gasto.Fecha),
                "La fecha es obligatoria.");
        }

        // Comprobar que el repartidor exista y esté activo.
        var repartidorValido = await _context.Repartidores
            .AnyAsync(r =>
                r.Id == gasto.IdRepartidor &&
                r.Estado &&
                r.EstadoRegistro);

        if (!repartidorValido)
        {
            ModelState.AddModelError(
                nameof(gasto.IdRepartidor),
                "Selecciona un repartidor activo.");
        }

if (!ModelState.IsValid)
{
    var errores = ModelState
        .Where(x => x.Value != null && x.Value.Errors.Count > 0)
        .SelectMany(x => x.Value!.Errors.Select(e =>
            $"{x.Key}: {e.ErrorMessage} {e.Exception?.Message}"))
        .ToList();

    Console.WriteLine("=== ERRORES DE VALIDACIÓN DE GASTOS ===");

    foreach (var error in errores)
    {
        Console.WriteLine(error);
    }

    await CargarRepartidoresSelectAsync();
    return PartialView("_Create", gasto);
}

        gasto.EstadoRegistro = true;
        gasto.FechaCreacion = DateTime.UtcNow;

        _context.GastosRuta.Add(gasto);
        await _context.SaveChangesAsync();

        TempData["Success"] = "GASTO REGISTRADO CORRECTAMENTE";

        return Json(new
        {
            success = true,
            message = TempData["Success"],
            redirectUrl = Url.Action(nameof(Index))
        });
    }

     // Mostrar el formulario para editar un gasto.
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var gasto = await _context.GastosRuta
            .AsNoTracking()
            .FirstOrDefaultAsync(g =>
                g.Id == id && g.EstadoRegistro);

        if (gasto == null)
        {
            return NotFound();
        }

        await CargarRepartidoresSelectAsync();

        return PartialView("_Edit", gasto);
    }

    // Preparar la lista de repartidores para el formulario.
    private async Task CargarRepartidoresSelectAsync()
    {
        var repartidores = await _context.Repartidores
            .AsNoTracking()
            .Where(r => r.Estado && r.EstadoRegistro)
            .OrderBy(r => r.Nombre)
            .ToListAsync();

        ViewBag.RepartidoresSelect = new SelectList(
            repartidores,
            nameof(Repartidor.Id),
            nameof(Repartidor.Nombre));
    }
    
    // Guardar los cambios de un gasto existente.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GastoRuta gasto)
    {
        var gastoExistente = await _context.GastosRuta
            .FirstOrDefaultAsync(g =>
                g.Id == gasto.Id && g.EstadoRegistro);

        if (gastoExistente == null)
        {
            return NotFound();
        }

        if (!TiposGastoRuta.Todos.Contains(gasto.Tipo))
        {
            ModelState.AddModelError(
                nameof(gasto.Tipo),
                "Selecciona un tipo de gasto válido.");
        }

        if (gasto.Monto <= 0)
        {
            ModelState.AddModelError(
                nameof(gasto.Monto),
                "El monto debe ser mayor que cero.");
        }

        if (gasto.Fecha == default)
        {
            ModelState.AddModelError(
                nameof(gasto.Fecha),
                "La fecha es obligatoria.");
        }

        var repartidorValido = await _context.Repartidores
            .AnyAsync(r =>
                r.Id == gasto.IdRepartidor &&
                r.Estado &&
                r.EstadoRegistro);

        if (!repartidorValido)
        {
            ModelState.AddModelError(
                nameof(gasto.IdRepartidor),
                "Selecciona un repartidor activo.");
        }

        if (!ModelState.IsValid)
        {
            await CargarRepartidoresSelectAsync();
            return PartialView("_Edit", gasto);
        }

        gastoExistente.IdRepartidor = gasto.IdRepartidor;
        gastoExistente.Tipo = gasto.Tipo;
        gastoExistente.Monto = gasto.Monto;
        gastoExistente.Fecha = gasto.Fecha;
        gastoExistente.Observacion = gasto.Observacion;
        gastoExistente.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "GASTO ACTUALIZADO CORRECTAMENTE",
            redirectUrl = Url.Action(nameof(Index))
        });
    }

}
