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
            Repartidores = await _context.Repartidores
                .Where(r => r.Estado && r.EstadoRegistro)
                .OrderBy(r => r.Nombre)
                .ToListAsync()
        };

        return View(modelo);
    }

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ObtenerParaEditar(long idPedido)
    {
        var pedido = await _context.Pedidos
            .Where(p => p.Id == idPedido && p.EstadoRegistro)
            .Select(p => new
            {
                idPedido = p.Id,
                idRepartidor = p.IdRepartidor,
                clientes = p.Clientes.Where(pc => pc.EstadoRegistro).Select(pc => new
                {
                    cliente = new
                    {
                        id = pc.Cliente.Id,
                        nombre = pc.Cliente.Nombre,
                        telefono = pc.Cliente.Telefono,
                        dni = pc.Cliente.Dni,
                        direcciones = pc.Cliente.Direcciones
                            .Where(d => d.EstadoRegistro)
                            .Select(d => new
                            {
                                id = d.Id,
                                direccion = d.Direccion,
                                ciudad = d.Ciudad,
                                urlUbicacion = d.UrlUbicacion,
                                principal = d.Principal
                            }).ToList()
                    },
                    idDireccion = pc.IdDireccion,
                    detalles = pc.Detalles.Where(d => d.EstadoRegistro).Select(d => new
                    {
                        idProducto = d.IdProducto,
                        cantidad = d.Cantidad
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync();

        return pedido == null ? NotFound() : Json(pedido);
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
    public async Task<IActionResult> ProductosDisponibles() =>
        Json(await _pedidoService.ProductosDisponiblesAsync());

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
    public async Task<IActionResult> Actualizar(long idPedido, [FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.ActualizarAsync(idPedido, modelo ?? new CrearPedidoViewModel());
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Eliminar(long idPedido)
    {
        var eliminado = await _pedidoService.EliminarAsync(idPedido);
        return eliminado ? Ok() : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> EnviarSeleccionados([FromBody] EnviarPedidosViewModel modelo)
    {
        var resultado = await _pedidoService.EnviarVariosAsync(modelo ?? new EnviarPedidosViewModel());
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
