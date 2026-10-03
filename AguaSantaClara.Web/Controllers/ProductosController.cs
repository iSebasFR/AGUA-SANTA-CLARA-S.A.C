using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora,Gerente,Vendedora")]
public class ProductosController : Controller
{
    private readonly AppDbContext _context;

    public ProductosController(AppDbContext context)
    {
        _context = context;
    }

    // ==================== INDEX CON FILTROS ====================
    public async Task<IActionResult> Index(string? search, string? categoria, bool? estado)
    {
        var consulta = _context.Productos
            .Where(p => p.EstadoRegistro)
            .AsQueryable();

        // Filtro por búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();
            consulta = consulta.Where(p => p.Nombre.Contains(termino));
        }

        // Filtro por categoría
        if (!string.IsNullOrWhiteSpace(categoria))
        {
            consulta = consulta.Where(p => p.Categoria == categoria);
        }

        // Filtro por estado
        if (estado.HasValue)
        {
            consulta = consulta.Where(p => p.Estado == estado.Value);
        }

        var productos = await consulta
            .OrderBy(p => p.Categoria)
            .ThenBy(p => p.Nombre)
            .ToListAsync();

        var model = new ProductosIndexViewModel
        {
            Productos = productos,
            Search = search,
            CategoriaFiltro = categoria,
            EstadoFiltro = estado
        };

        return View(model);
    }

    // ==================== CREATE ====================
    [HttpGet]
    [Authorize(Roles = "Administradora,Gerente")]
    public IActionResult Create()
    {
        return PartialView("_Create", new CrearProductoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Create(CrearProductoViewModel model)
    {
        // Validar nombre duplicado
        var nombreDuplicado = await _context.Productos
            .AnyAsync(p => p.Nombre == model.Nombre.Trim() && p.EstadoRegistro);

        if (nombreDuplicado)
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un producto con ese nombre.");

        if (!ModelState.IsValid)
            return PartialView("_Create", model);

        var producto = new Producto
        {
            Nombre = model.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim(),
            Categoria = model.Categoria,
            PrecioVenta = model.PrecioVenta,
            Costo = model.Costo,
            Estado = model.Estado,
            EstadoRegistro = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== EDIT ====================
    [HttpGet]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(long id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        var model = new EditarProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Categoria = producto.Categoria ?? string.Empty,
            PrecioVenta = producto.PrecioVenta,
            Costo = producto.Costo
        };

        return PartialView("_Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(EditarProductoViewModel model)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == model.Id && p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        // Validar nombre duplicado (excepto el mismo producto)
        var nombreDuplicado = await _context.Productos
            .AnyAsync(p => p.Nombre == model.Nombre.Trim() && p.Id != model.Id && p.EstadoRegistro);

        if (nombreDuplicado)
            ModelState.AddModelError(nameof(model.Nombre), "Ya existe un producto con ese nombre.");

        if (!ModelState.IsValid)
            return PartialView("_Edit", model);

        producto.Nombre = model.Nombre.Trim();
        producto.Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim();
        producto.Categoria = model.Categoria;
        producto.PrecioVenta = model.PrecioVenta;
        producto.Costo = model.Costo;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== CAMBIAR ESTADO ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> CambiarEstado(long id, bool estado)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        producto.Estado = estado;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = estado
            ? "PRODUCTO ACTIVADO CORRECTAMENTE"
            : "PRODUCTO DESACTIVADO CORRECTAMENTE";

        return RedirectToAction(nameof(Index));
    }

    // ==================== ELIMINAR ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Delete(long id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO ELIMINADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }
}