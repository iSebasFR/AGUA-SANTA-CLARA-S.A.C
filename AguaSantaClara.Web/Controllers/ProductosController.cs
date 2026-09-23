using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora")]
public class ProductosController : Controller
{
    private readonly AppDbContext _context;

    public ProductosController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var consulta = _context.Productos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var termino = search.Trim();

            consulta = consulta.Where(p =>
                p.Nombre.Contains(termino));
        }

        var productos = await consulta
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        ViewBag.Search = search;

        return View(productos);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CrearProductoViewModel());
    }

    [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(CrearProductoViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var producto = new Producto
    {
        Nombre = model.Nombre.Trim(),
        Descripcion = string.IsNullOrWhiteSpace(model.Descripcion)
            ? null
            : model.Descripcion.Trim(),
        PrecioVenta = model.PrecioVenta,
        Costo = model.Costo,
        StockActual = model.StockActual,
        StockMinimo = model.StockMinimo,
        Estado = model.Estado,
        EstadoRegistro = true
    };

    _context.Productos.Add(producto);
    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "PRODUCTO CREADO CORRECTAMENTE";

    return RedirectToAction(nameof(Index));
}

[HttpGet]
public async Task<IActionResult> Edit(long id)
{
    var producto = await _context.Productos
        .FirstOrDefaultAsync(p => p.Id == id);

    if (producto == null)
        return NotFound();

    var model = new EditarProductoViewModel
    {
        Id = producto.Id,
        Nombre = producto.Nombre,
        Descripcion = producto.Descripcion,
        PrecioVenta = producto.PrecioVenta,
        Costo = producto.Costo,
        StockActual = producto.StockActual,
        StockMinimo = producto.StockMinimo,
        Estado = producto.Estado
    };

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(EditarProductoViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var producto = await _context.Productos
        .FirstOrDefaultAsync(p => p.Id == model.Id);

    if (producto == null)
        return NotFound();

    producto.Nombre = model.Nombre.Trim();
    producto.Descripcion = string.IsNullOrWhiteSpace(model.Descripcion)
        ? null
        : model.Descripcion.Trim();
    producto.PrecioVenta = model.PrecioVenta;
    producto.Costo = model.Costo;
    producto.StockActual = model.StockActual;
    producto.StockMinimo = model.StockMinimo;
    producto.Estado = model.Estado;

    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "PRODUCTO ACTUALIZADO CORRECTAMENTE";

    return RedirectToAction(nameof(Index));
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Deactivate(long id)
{
    var producto = await _context.Productos
        .FirstOrDefaultAsync(p => p.Id == id);

    if (producto == null)
        return NotFound();

    producto.Estado = false;

    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "PRODUCTO DESACTIVADO CORRECTAMENTE";

    return RedirectToAction(nameof(Index));
}
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Activate(long id)
{
    var producto = await _context.Productos
        .FirstOrDefaultAsync(p => p.Id == id);

    if (producto == null)
        return NotFound();

    producto.Estado = true;

    await _context.SaveChangesAsync();

    TempData["SuccessMessage"] = "PRODUCTO ACTIVADO CORRECTAMENTE";

    return RedirectToAction(nameof(Index));
}

}