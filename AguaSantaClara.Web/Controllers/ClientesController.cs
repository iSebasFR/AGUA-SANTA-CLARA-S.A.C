using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

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
    public IActionResult Create() => View(new CrearClienteViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrearClienteViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var cliente = new Cliente
        {
            Nombre = model.Nombre.Trim(),
            Telefono = model.Telefono.Trim(),
            Estado = model.Estado,
            EstadoRegistro = true
        };

        cliente.Direcciones.Add(new DireccionCliente
        {
            Direccion = model.Direccion.Trim(),
            Principal = true,
            Estado = true,
            EstadoRegistro = true
        });

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cliente creado correctamente";
        return RedirectToAction(nameof(Index));
    }
}