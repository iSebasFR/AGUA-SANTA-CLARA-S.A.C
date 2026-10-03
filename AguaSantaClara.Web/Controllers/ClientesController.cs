using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora,Gerente,Administradora")]
public class ClientesController : Controller
{
    private readonly AppDbContext _context;

    public ClientesController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var consulta = _context.Clientes
            .Include(cliente => cliente.Direcciones.Where(direccion => direccion.EstadoRegistro))
            .Where(cliente => cliente.EstadoRegistro);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var patron = $"%{search.Trim()}%";
            consulta = consulta.Where(cliente =>
                EF.Functions.ILike(cliente.Nombre, patron) ||
                EF.Functions.ILike(cliente.Telefono, patron));
        }

        var clientes = await consulta
            .OrderBy(cliente => cliente.Nombre)
            .ToListAsync();

        ViewBag.Search = search;
        return View(clientes);
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public IActionResult Create() => View(new CrearClienteViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Create(CrearClienteViewModel model)
    {
        var dni = string.IsNullOrWhiteSpace(model.Dni) ? null : model.Dni.Trim();
        if (dni != null && await DniDuplicadoAsync(dni, null))
            ModelState.AddModelError(nameof(model.Dni), "Ya existe un cliente con ese DNI.");

        if (!ModelState.IsValid)
            return View(model);

        var cliente = new Cliente
        {
            Nombre = model.Nombre.Trim(),
            Telefono = model.Telefono.Trim(),
            Dni = dni,
            Estado = model.Estado,
            EstadoRegistro = true
        };

        cliente.Direcciones.Add(new DireccionCliente
        {
            Direccion = model.Direccion.Trim(),
            Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim(),
            Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
            UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim(),
            Principal = true,
            Estado = true,
            EstadoRegistro = true
        });

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cliente creado correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var cliente = await _context.Clientes
            .Include(cliente => cliente.Direcciones.Where(direccion => direccion.EstadoRegistro))
            .FirstOrDefaultAsync(cliente => cliente.Id == id && cliente.EstadoRegistro);

        return cliente == null ? NotFound() : View(cliente);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var cliente = await _context.Clientes
            .Include(cliente => cliente.Direcciones.Where(direccion => direccion.EstadoRegistro))
            .FirstOrDefaultAsync(cliente => cliente.Id == id && cliente.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        var direccionPrincipal = cliente.Direcciones
            .OrderByDescending(direccion => direccion.Principal)
            .FirstOrDefault();

        return View(new EditarClienteViewModel
        {
            Id = cliente.Id,
            Nombre = cliente.Nombre,
            Telefono = cliente.Telefono,
            Dni = cliente.Dni,
            Direccion = direccionPrincipal?.Direccion ?? string.Empty,
            Ciudad = direccionPrincipal?.Ciudad,
            Referencia = direccionPrincipal?.Referencia,
            UrlUbicacion = direccionPrincipal?.UrlUbicacion,
            Estado = cliente.Estado
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditarClienteViewModel model)
    {
        var dni = string.IsNullOrWhiteSpace(model.Dni) ? null : model.Dni.Trim();
        if (dni != null && await DniDuplicadoAsync(dni, model.Id))
            ModelState.AddModelError(nameof(model.Dni), "Ya existe un cliente con ese DNI.");

        if (!ModelState.IsValid)
            return View(model);

        var cliente = await _context.Clientes
            .Include(cliente => cliente.Direcciones.Where(direccion => direccion.EstadoRegistro))
            .FirstOrDefaultAsync(cliente => cliente.Id == model.Id && cliente.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        cliente.Nombre = model.Nombre.Trim();
        cliente.Telefono = model.Telefono.Trim();
        cliente.Dni = dni;
        cliente.Estado = model.Estado;

        var direccionPrincipal = cliente.Direcciones
            .OrderByDescending(direccion => direccion.Principal)
            .FirstOrDefault();

        if (direccionPrincipal == null)
        {
            direccionPrincipal = new DireccionCliente
            {
                Direccion = model.Direccion.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim(),
                Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
                UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim(),
                Principal = true,
                Estado = true,
                EstadoRegistro = true
            };
            cliente.Direcciones.Add(direccionPrincipal);
        }
        else
        {
            direccionPrincipal.Direccion = model.Direccion.Trim();
            direccionPrincipal.Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim();
            direccionPrincipal.Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim();
            direccionPrincipal.UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim();
        }

        foreach (var direccion in cliente.Direcciones)
            direccion.Principal = direccion == direccionPrincipal;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cliente actualizado correctamente";
        return RedirectToAction(nameof(Details), new { id = cliente.Id });
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> AddAddress(long id)
    {
        var clienteExiste = await _context.Clientes
            .AnyAsync(cliente => cliente.Id == id && cliente.EstadoRegistro);

        if (!clienteExiste)
            return NotFound();

        var tieneDirecciones = await _context.DireccionesCliente
            .AnyAsync(direccion => direccion.IdCliente == id && direccion.EstadoRegistro);

        return View(new DireccionClienteViewModel
        {
            IdCliente = id,
            Principal = !tieneDirecciones
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> AddAddress(DireccionClienteViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var clienteExiste = await _context.Clientes
            .AnyAsync(cliente => cliente.Id == model.IdCliente && cliente.EstadoRegistro);

        if (!clienteExiste)
            return NotFound();

        var direcciones = await _context.DireccionesCliente
            .Where(direccion => direccion.IdCliente == model.IdCliente && direccion.EstadoRegistro)
            .ToListAsync();
        var seraPrincipal = model.Principal || direcciones.Count == 0;

        if (seraPrincipal)
        {
            foreach (var direccion in direcciones)
                direccion.Principal = false;
        }

        _context.DireccionesCliente.Add(new DireccionCliente
        {
            IdCliente = model.IdCliente,
            Direccion = model.Direccion.Trim(),
            Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim(),
            Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim(),
            UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim(),
            Principal = seraPrincipal,
            Estado = true,
            EstadoRegistro = true
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Dirección agregada correctamente";
        return RedirectToAction(nameof(Details), new { id = model.IdCliente });
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> EditAddress(long id)
    {
        var direccion = await _context.DireccionesCliente
            .FirstOrDefaultAsync(direccion => direccion.Id == id && direccion.EstadoRegistro);

        if (direccion == null)
            return NotFound();

        return View(new DireccionClienteViewModel
        {
            Id = direccion.Id,
            IdCliente = direccion.IdCliente,
            Direccion = direccion.Direccion,
            Ciudad = direccion.Ciudad,
            Referencia = direccion.Referencia,
            UrlUbicacion = direccion.UrlUbicacion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> EditAddress(DireccionClienteViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var direccion = await _context.DireccionesCliente
            .FirstOrDefaultAsync(direccion =>
                direccion.Id == model.Id &&
                direccion.IdCliente == model.IdCliente &&
                direccion.EstadoRegistro);

        if (direccion == null)
            return NotFound();

        direccion.Direccion = model.Direccion.Trim();
        direccion.Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim();
        direccion.Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim();
        direccion.UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim();

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Dirección actualizada correctamente";
        return RedirectToAction(nameof(Details), new { id = model.IdCliente });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> SetPrimaryAddress(long id, long clienteId)
    {
        var direcciones = await _context.DireccionesCliente
            .Where(direccion => direccion.IdCliente == clienteId && direccion.EstadoRegistro)
            .ToListAsync();
        var direccionPrincipal = direcciones.FirstOrDefault(direccion => direccion.Id == id);

        if (direccionPrincipal == null)
            return NotFound();

        foreach (var direccion in direcciones)
            direccion.Principal = direccion == direccionPrincipal;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Dirección principal actualizada correctamente";
        return RedirectToAction(nameof(Details), new { id = clienteId });
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Delete(long id)
    {
        var cliente = await _context.Clientes
            .Include(cliente => cliente.Direcciones.Where(direccion => direccion.EstadoRegistro))
            .FirstOrDefaultAsync(cliente => cliente.Id == id && cliente.EstadoRegistro);

        return cliente == null ? NotFound() : View(cliente);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(cliente => cliente.Id == id && cliente.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cliente eliminado correctamente";
        return RedirectToAction(nameof(Index));
    }

    private Task<bool> DniDuplicadoAsync(string dni, long? excluirId) =>
        _context.Clientes.AnyAsync(cliente => cliente.Dni == dni && cliente.Id != excluirId);
}
