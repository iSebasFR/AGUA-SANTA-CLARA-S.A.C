using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public async Task<IActionResult> Create()
    {
        if (!EsSolicitudAjax)
            return RedirectToAction(nameof(Index));

        var model = new CrearProductoViewModel
        {
            Locales = await _context.Locales
                .Where(l => l.EstadoRegistro && l.Estado)
                .OrderBy(l => l.Nombre)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Nombre
                })
                .ToListAsync()
        };

        return PartialView("_Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Create(CrearProductoViewModel model)
    {
        model.Locales = await _context.Locales
            .Where(l => l.EstadoRegistro && l.Estado)
            .OrderBy(l => l.Nombre)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.Nombre
            })
            .ToListAsync();

        model.IdLocales = (model.IdLocales ?? new List<long>()).Distinct().ToList();
        if (model.IdLocales.Count == 0)
            ModelState.AddModelError(nameof(model.IdLocales), "Seleccione al menos un local.");

        var localesValidos = await _context.Locales
            .Where(l => model.IdLocales.Contains(l.Id) && l.EstadoRegistro && l.Estado)
            .Select(l => l.Id)
            .ToListAsync();

        if (localesValidos.Count != model.IdLocales.Count)
            ModelState.AddModelError(nameof(model.IdLocales), "Seleccione únicamente locales activos válidos.");

        var productoExistente = await _context.Productos
            .FirstOrDefaultAsync(p => p.Nombre == model.Nombre.Trim() && p.EstadoRegistro);
        var relacionesExistentes = new List<ProductoLocal>();

        if (productoExistente != null)
        {
            if (!productoExistente.Estado)
                ModelState.AddModelError(nameof(model.Nombre), "El producto existe, pero está inactivo. Actívalo desde Editar antes de agregarlo a otro local.");

            relacionesExistentes = await _context.ProductosLocal
                .Where(pl => pl.IdProducto == productoExistente.Id && model.IdLocales.Contains(pl.IdLocal))
                .ToListAsync();

            var todosYaAsignados = model.IdLocales.Count > 0 && model.IdLocales.All(idLocal =>
                relacionesExistentes.Any(pl => pl.IdLocal == idLocal && pl.Estado && pl.EstadoRegistro));

            if (todosYaAsignados)
                ModelState.AddModelError(nameof(model.IdLocales), "Este producto ya está registrado en todos los locales seleccionados.");
        }

        if (!ModelState.IsValid)
        {
            if (EsSolicitudAjax)
                return PartialView("_Create", model);

            TempData["ErrorMessage"] = ObtenerErroresModelState();
            return RedirectToAction(nameof(Index));
        }

        if (productoExistente != null)
        {
            foreach (var idLocal in model.IdLocales)
            {
                var relacion = relacionesExistentes.FirstOrDefault(pl => pl.IdLocal == idLocal);
                if (relacion == null)
                {
                    _context.ProductosLocal.Add(new ProductoLocal
                    {
                        IdLocal = idLocal,
                        Producto = productoExistente,
                        Stock = 0,
                        Estado = true,
                        EstadoRegistro = true
                    });
                }
                else
                {
                    relacion.Estado = true;
                    relacion.EstadoRegistro = true;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "PRODUCTO AGREGADO A LOS LOCALES SELECCIONADOS";
            return RedirectToAction(nameof(Index));
        }

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
        foreach (var idLocal in model.IdLocales)
        {
            _context.ProductosLocal.Add(new ProductoLocal
            {
                IdLocal = idLocal,
                Producto = producto,
                Stock = 0,
                Estado = true,
                EstadoRegistro = true
            });
        }
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO CREADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    // ==================== EDIT ====================
    [HttpGet]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(long id)
    {
        if (!EsSolicitudAjax)
            return RedirectToAction(nameof(Index));

        var producto = await _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.EstadoRegistro);

        if (producto == null)
            return NotFound();

        var localesActuales = await _context.ProductosLocal
            .Where(pl => pl.IdProducto == producto.Id && pl.Estado && pl.EstadoRegistro)
            .Select(pl => pl.IdLocal)
            .ToListAsync();

        var model = new EditarProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Categoria = producto.Categoria == "Papel Higiénico" ? "Papel" : producto.Categoria ?? string.Empty,
            PrecioVenta = producto.PrecioVenta,
            Costo = producto.Costo,
            IdLocales = localesActuales,
            Locales = await _context.Locales
                .Where(l => l.Estado && l.EstadoRegistro)
                .OrderBy(l => l.Nombre)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(),
                    Text = l.Nombre
                })
                .ToListAsync()
        };

        return PartialView("_Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administradora,Gerente")]
    public async Task<IActionResult> Edit(EditarProductoViewModel model)
    {
        model.Locales = await _context.Locales
            .Where(l => l.Estado && l.EstadoRegistro)
            .OrderBy(l => l.Nombre)
            .Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = l.Nombre
            })
            .ToListAsync();

        model.IdLocales = (model.IdLocales ?? new List<long>()).Distinct().ToList();
        if (model.IdLocales.Count == 0)
            ModelState.AddModelError(nameof(model.IdLocales), "Seleccione al menos un local.");

        var localesValidos = await _context.Locales
            .Where(l => model.IdLocales.Contains(l.Id) && l.Estado && l.EstadoRegistro)
            .Select(l => l.Id)
            .ToListAsync();

        if (localesValidos.Count != model.IdLocales.Count)
            ModelState.AddModelError(nameof(model.IdLocales), "Seleccione únicamente locales activos válidos.");

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
        {
            if (EsSolicitudAjax)
                return PartialView("_Edit", model);

            TempData["ErrorMessage"] = ObtenerErroresModelState();
            return RedirectToAction(nameof(Index));
        }

        var relacionesProducto = await _context.ProductosLocal
            .Where(pl => pl.IdProducto == producto.Id)
            .ToListAsync();

        foreach (var relacion in relacionesProducto.Where(pl => !model.IdLocales.Contains(pl.IdLocal)))
        {
            relacion.Estado = false;
            relacion.EstadoRegistro = false;
        }

        foreach (var idLocal in model.IdLocales)
        {
            var relacion = relacionesProducto.FirstOrDefault(pl => pl.IdLocal == idLocal);
            if (relacion == null)
            {
                _context.ProductosLocal.Add(new ProductoLocal
                {
                    IdLocal = idLocal,
                    Producto = producto,
                    Stock = 0,
                    Estado = true,
                    EstadoRegistro = true
                });
            }
            else
            {
                relacion.Estado = true;
                relacion.EstadoRegistro = true;
            }
        }

        producto.Nombre = model.Nombre.Trim();
        producto.Descripcion = string.IsNullOrWhiteSpace(model.Descripcion) ? null : model.Descripcion.Trim();
        producto.Categoria = model.Categoria;
        producto.PrecioVenta = model.PrecioVenta;
        producto.Costo = model.Costo;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO ACTUALIZADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }

    private bool EsSolicitudAjax => string.Equals(
        Request.Headers["X-Requested-With"].ToString(),
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

    private string ObtenerErroresModelState() => string.Join(" ", ModelState.Values
        .SelectMany(value => value.Errors)
        .Select(error => error.ErrorMessage)
        .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
        .Distinct());

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

        // 1. Eliminar primero los registros relacionados en ProductoLocal
        var productosLocal = await _context.ProductosLocal
            .Where(pl => pl.IdProducto == id)
            .ToListAsync();

        if (productosLocal.Any())
        {
            _context.ProductosLocal.RemoveRange(productosLocal);
        }

        // 2. Ahora eliminar el producto
        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "PRODUCTO ELIMINADO CORRECTAMENTE";
        return RedirectToAction(nameof(Index));
    }
}