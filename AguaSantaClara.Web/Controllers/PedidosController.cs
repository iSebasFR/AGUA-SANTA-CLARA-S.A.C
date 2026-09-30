using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora,Gerente,Administradora")]
public class PedidosController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPedidoService _pedidoService;

    public PedidosController(AppDbContext context, IPedidoService pedidoService)
    {
        _context = context;
        _pedidoService = pedidoService;
    }

    public async Task<IActionResult> Index()
    {
        var modelo = new PedidosIndexViewModel
        {
            Pedidos = await _context.Pedidos
                .Include(p => p.Local)
                .Include(p => p.Repartidor)
                .Include(p => p.Clientes).ThenInclude(c => c.Cliente)
                .Where(p => p.EstadoRegistro)
                .OrderByDescending(p => p.Id)
                .ToListAsync(),
            Locales = await _context.Locales
                .Where(l => l.Estado && l.EstadoRegistro)
                .OrderBy(l => l.Nombre)
                .ToListAsync(),
            Repartidores = await _context.Repartidores
                .Where(r => r.Estado && r.EstadoRegistro)
                .OrderBy(r => r.Nombre)
                .ToListAsync()
        };

        return View(modelo);
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> BuscarCliente(string q)
    {
        var cliente = await _pedidoService.BuscarClienteAsync(q);
        return cliente == null
            ? NotFound(new { mensaje = "No se encontró un cliente con ese teléfono o DNI." })
            : Json(cliente);
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ProductosPorLocal(long idLocal) =>
        Json(await _pedidoService.ProductosPorLocalAsync(idLocal));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Guardar([FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.CrearAsync(modelo ?? new CrearPedidoViewModel(), enviar: false);
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Enviar([FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.CrearAsync(modelo ?? new CrearPedidoViewModel(), enviar: true);
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }
}
