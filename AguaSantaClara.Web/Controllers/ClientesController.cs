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

    // ==================== INDEX CON FILTROS ====================
    public async Task<IActionResult> Index(string? search, bool? estado)
    {
        var consulta = _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .Where(c => c.EstadoRegistro)
            .AsQueryable();

        // Filtro por búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            var patron = $"%{search.Trim()}%";
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Nombre, patron) ||
                EF.Functions.ILike(c.Telefono, patron) ||
                (c.Dni != null && EF.Functions.ILike(c.Dni, patron)));
        }

        // Filtro por estado
        if (estado.HasValue)
        {
            consulta = consulta.Where(c => c.Estado == estado.Value);
        }

        var clientes = await consulta
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        var model = new ClientesIndexViewModel
        {
            Clientes = clientes,
            Search = search,
            EstadoFiltro = estado
        };

        return View(model);
    }

    // ==================== DETAILS (FICHA DEL CLIENTE) ====================
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .FirstOrDefaultAsync(c => c.Id == id && c.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        var pedidos = await _context.Pedidos
            .Include(p => p.Local)
            .Include(p => p.Repartidor)
            .Include(p => p.Clientes).ThenInclude(pc => pc.Detalles)
            .AsSplitQuery()
            .Where(p => p.EstadoRegistro && p.Clientes.Any(pc => pc.IdCliente == cliente.Id))
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        ViewBag.Pedidos = pedidos;
        return View(cliente);
    }

    // ==================== CREATE (MODAL) ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public IActionResult Create()
    {
        return PartialView("_Create", new CrearClienteViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Create(CrearClienteViewModel model)
    {
        var dni = string.IsNullOrWhiteSpace(model.Dni) ? null : model.Dni.Trim();
        if (dni != null && await DniDuplicadoAsync(dni, null))
            ModelState.AddModelError(nameof(model.Dni), "Ya existe un cliente con ese DNI.");

        if (!ModelState.IsValid)
            return PartialView("_Create", model);

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

        TempData["SuccessMessage"] = "CLIENTE CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== EDIT (MODAL) ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora,Gerente,Administradora")]
    public async Task<IActionResult> Edit(long id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .FirstOrDefaultAsync(c => c.Id == id && c.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        var direccionPrincipal = cliente.Direcciones
            .OrderByDescending(d => d.Principal)
            .FirstOrDefault();

        var model = new EditarClienteViewModel
        {
            Id = cliente.Id,
            Nombre = cliente.Nombre,
            Telefono = cliente.Telefono,
            Dni = cliente.Dni,
            Direccion = direccionPrincipal?.Direccion ?? string.Empty,
            Ciudad = direccionPrincipal?.Ciudad,
            Referencia = direccionPrincipal?.Referencia,
            UrlUbicacion = direccionPrincipal?.UrlUbicacion
        };

        return PartialView("_Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora,Gerente,Administradora")]
    public async Task<IActionResult> Edit(EditarClienteViewModel model)
    {
        var dni = string.IsNullOrWhiteSpace(model.Dni) ? null : model.Dni.Trim();
        if (dni != null && await DniDuplicadoAsync(dni, model.Id))
            ModelState.AddModelError(nameof(model.Dni), "Ya existe un cliente con ese DNI.");

        if (!ModelState.IsValid)
            return PartialView("_Edit", model);

        var cliente = await _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .FirstOrDefaultAsync(c => c.Id == model.Id && c.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        cliente.Nombre = model.Nombre.Trim();
        cliente.Telefono = model.Telefono.Trim();
        cliente.Dni = dni;

        var direccionPrincipal = cliente.Direcciones
            .OrderByDescending(d => d.Principal)
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

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "CLIENTE ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== CAMBIAR ESTADO ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora,Gerente,Administradora")]
    public async Task<IActionResult> CambiarEstado(long id, bool estado)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        cliente.Estado = estado;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = estado
            ? "CLIENTE ACTIVADO CORRECTAMENTE"
            : "CLIENTE DESACTIVADO CORRECTAMENTE";

        return RedirectToAction(nameof(Index));
    }

    // ==================== ELIMINAR ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora,Gerente,Administradora")]
    public async Task<IActionResult> Delete(long id)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id && c.EstadoRegistro);

        if (cliente == null)
            return NotFound();

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "CLIENTE ELIMINADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== DIRECCIONES (MODAL) ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> AddAddress(long id)
    {
        var clienteExiste = await _context.Clientes
            .AnyAsync(c => c.Id == id && c.EstadoRegistro);

        if (!clienteExiste)
            return NotFound();

        var tieneDirecciones = await _context.DireccionesCliente
            .AnyAsync(d => d.IdCliente == id && d.EstadoRegistro);

        return PartialView("_AddAddress", new DireccionClienteViewModel
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
            return PartialView("_AddAddress", model);

        var clienteExiste = await _context.Clientes
            .AnyAsync(c => c.Id == model.IdCliente && c.EstadoRegistro);

        if (!clienteExiste)
            return NotFound();

        var direcciones = await _context.DireccionesCliente
            .Where(d => d.IdCliente == model.IdCliente && d.EstadoRegistro)
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
        TempData["SuccessMessage"] = "DIRECCIÓN AGREGADA CORRECTAMENTE";
        return RedirectToAction(nameof(Details), new { id = model.IdCliente });
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> EditAddress(long id)
    {
        var direccion = await _context.DireccionesCliente
            .FirstOrDefaultAsync(d => d.Id == id && d.EstadoRegistro);

        if (direccion == null)
            return NotFound();

        return PartialView("_EditAddress", new DireccionClienteViewModel
        {
            Id = direccion.Id,
            IdCliente = direccion.IdCliente,
            Direccion = direccion.Direccion,
            Ciudad = direccion.Ciudad,
            Referencia = direccion.Referencia,
            UrlUbicacion = direccion.UrlUbicacion,
            Principal = direccion.Principal
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> EditAddress(DireccionClienteViewModel model)
    {
        if (!ModelState.IsValid)
            return PartialView("_EditAddress", model);

        var direccion = await _context.DireccionesCliente
            .FirstOrDefaultAsync(d =>
                d.Id == model.Id &&
                d.IdCliente == model.IdCliente &&
                d.EstadoRegistro);

        if (direccion == null)
            return NotFound();

        direccion.Direccion = model.Direccion.Trim();
        direccion.Ciudad = string.IsNullOrWhiteSpace(model.Ciudad) ? null : model.Ciudad.Trim();
        direccion.Referencia = string.IsNullOrWhiteSpace(model.Referencia) ? null : model.Referencia.Trim();
        direccion.UrlUbicacion = string.IsNullOrWhiteSpace(model.UrlUbicacion) ? null : model.UrlUbicacion.Trim();

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "DIRECCIÓN ACTUALIZADA CORRECTAMENTE";
        return RedirectToAction(nameof(Details), new { id = model.IdCliente });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> SetPrimaryAddress(long id, long clienteId)
    {
        var direcciones = await _context.DireccionesCliente
            .Where(d => d.IdCliente == clienteId && d.EstadoRegistro)
            .ToListAsync();
        var direccionPrincipal = direcciones.FirstOrDefault(d => d.Id == id);

        if (direccionPrincipal == null)
            return NotFound();

        foreach (var direccion in direcciones)
            direccion.Principal = direccion == direccionPrincipal;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "DIRECCIÓN PRINCIPAL ACTUALIZADA CORRECTAMENTE";
        return RedirectToAction(nameof(Details), new { id = clienteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> DeleteAddress(long id, long clienteId)
    {
        var direccion = await _context.DireccionesCliente
            .FirstOrDefaultAsync(d => d.Id == id && d.IdCliente == clienteId && d.EstadoRegistro);

        if (direccion == null)
            return NotFound();

        // No permitir eliminar la única dirección del cliente
        var total = await _context.DireccionesCliente
            .CountAsync(d => d.IdCliente == clienteId && d.EstadoRegistro);

        if (total <= 1)
        {
            TempData["ErrorMessage"] = "No se puede eliminar la única dirección del cliente.";
            return RedirectToAction(nameof(Details), new { id = clienteId });
        }

        _context.DireccionesCliente.Remove(direccion);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "DIRECCIÓN ELIMINADA CORRECTAMENTE";
        return RedirectToAction(nameof(Details), new { id = clienteId });
    }

    private Task<bool> DniDuplicadoAsync(string dni, long? excluirId) =>
        _context.Clientes.AnyAsync(c => c.Dni == dni && c.Id != excluirId);
}