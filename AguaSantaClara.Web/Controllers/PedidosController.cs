using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
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

    public async Task<IActionResult> Index(
        string? estado,
        long? idCliente,
        long? idRepartidor)
    {
        var filtrosActivos = (!string.IsNullOrWhiteSpace(estado) ? 1 : 0)
            + (idCliente.HasValue ? 1 : 0)
            + (idRepartidor.HasValue ? 1 : 0);

        var errorFiltros = !string.IsNullOrWhiteSpace(estado)
            && !EstadosPedido.Todos.Contains(estado, StringComparer.OrdinalIgnoreCase)
                ? "Selecciona un estado válido."
                : null;

        var consultaPedidos = _context.Pedidos.Where(p => p.EstadoRegistro);
        if (errorFiltros == null)
        {
            if (!string.IsNullOrWhiteSpace(estado))
                consultaPedidos = consultaPedidos.Where(p => p.Estado == estado);
            if (idCliente.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.Clientes.Any(pc => pc.EstadoRegistro && pc.IdCliente == idCliente.Value));
            if (idRepartidor.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.IdRepartidor == idRepartidor.Value);
        }

        var repartidoresActivos = await _context.Repartidores
            .Where(r => r.Estado && r.EstadoRegistro)
            .OrderBy(r => r.Nombre)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        var repartidoresDisponibles = repartidoresActivos
            .GroupBy(r => r.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo => grupo.First())
            .OrderBy(r => r.Nombre)
            .ToList();

        var modelo = new PedidosIndexViewModel
        {
            Pedidos = await consultaPedidos
                .Include(p => p.Repartidor)
                .Include(p => p.Clientes).ThenInclude(c => c.Cliente)
                .Include(p => p.Clientes).ThenInclude(c => c.Direccion)
                .Include(p => p.Clientes).ThenInclude(c => c.Detalles).ThenInclude(d => d.Producto)
                .Include(p => p.Clientes).ThenInclude(c => c.Detalles).ThenInclude(d => d.Local)
                .AsSplitQuery()
                .OrderByDescending(p => p.Id)
                .ToListAsync(),
            Locales = await _context.Locales
                .Where(l => l.Estado && l.EstadoRegistro)
                .OrderBy(l => l.Nombre)
                .ToListAsync(),
            Repartidores = repartidoresDisponibles,
            RepartidoresFiltro = await _context.Repartidores
                .Where(r => r.EstadoRegistro)
                .OrderBy(r => r.Nombre)
                .ToListAsync(),
            ClientesFiltro = await _context.Clientes
                .Where(c => c.EstadoRegistro)
                .OrderBy(c => c.Nombre)
                .ToListAsync(),
            EstadoFiltro = estado,
            IdClienteFiltro = idCliente,
            IdRepartidorFiltro = idRepartidor,
            ErrorFiltros = errorFiltros,
            TieneFiltrosAplicados = filtrosActivos > 0 && errorFiltros == null
        };

        return View(modelo);
    }

    private static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

    // ==================== OBTENER PARA EDITAR ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ObtenerParaEditar(long idPedido)
    {
        var pedido = await _context.Pedidos
            .Where(p => p.Id == idPedido && p.EstadoRegistro)
            .Select(p => new
            {
                idPedido = p.Id,
                estado = p.Estado,
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
                        idLocal = d.IdLocal,
                        idProducto = d.IdProducto,
                        cantidad = d.Cantidad,
                        descuentoMonto = d.DescuentoMonto
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync();

        return pedido == null ? NotFound() : Json(pedido);
    }

    // ==================== BUSCAR CLIENTE ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> BuscarCliente(string tipo, string q)
    {
        var cliente = await _pedidoService.BuscarClienteAsync(tipo, q);
        return cliente == null
            ? NotFound(new { mensaje = "No se encontró un cliente con esos criterios." })
            : Json(cliente);
    }

    // ==================== AUTOCOMPLETADO ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> BuscarClientes(string q, int limite = 8)
    {
        var valor = q?.Trim();
        if (string.IsNullOrWhiteSpace(valor) || valor.Length < 2)
            return Json(Array.Empty<object>());

        var clientes = await _context.Clientes
            .Where(c => c.Estado && c.EstadoRegistro)
            .Where(c =>
                EF.Functions.ILike(c.Nombre, $"%{valor}%") ||
                EF.Functions.ILike(c.Telefono, $"%{valor}%") ||
                (c.Dni != null && EF.Functions.ILike(c.Dni, $"%{valor}%")))
            .OrderBy(c => c.Nombre)
            .Take(Math.Clamp(limite, 1, 20))
            .Select(c => new
            {
                id = c.Id,
                nombre = c.Nombre,
                telefono = c.Telefono,
                dni = c.Dni
            })
            .ToListAsync();

        return Json(clientes);
    }

    // ==================== CLIENTE POR ID ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ClientePorId(long id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .FirstOrDefaultAsync(c => c.Id == id && c.Estado && c.EstadoRegistro);

        if (cliente == null) return NotFound();

        return Json(new
        {
            id = cliente.Id,
            nombre = cliente.Nombre,
            telefono = cliente.Telefono,
            dni = cliente.Dni,
            direcciones = cliente.Direcciones
                .OrderByDescending(d => d.Principal)
                .ThenBy(d => d.Id)
                .Select(d => new
                {
                    id = d.Id,
                    direccion = d.Direccion,
                    ciudad = d.Ciudad,
                    urlUbicacion = d.UrlUbicacion,
                    principal = d.Principal
                })
                .ToList()
        });
    }

    // ==================== PRODUCTOS POR LOCAL ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ProductosPorLocal(long idLocal) =>
        Json(await _pedidoService.ProductosPorLocalAsync(idLocal));

    // ==================== GUARDAR (borrador) ====================
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

        if (!resultado.Ok)
            return BadRequest(resultado);

        return Json(new
        {
            ok = true,
            mensaje = "PEDIDO ACTUALIZADO CORRECTAMENTE"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Eliminar(long idPedido)
    {
        var eliminado = await _pedidoService.EliminarAsync(idPedido);
        return eliminado
            ? Json(new { ok = true, mensaje = "PEDIDO ELIMINADO CORRECTAMENTE" })
            : NotFound();
    }

    // ==================== CAMBIAR ESTADO ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> CambiarEstado(long idPedido, [FromBody] ActualizarEstadoPedidoViewModel? modelo)
    {
        var resultado = await _pedidoService.CambiarEstadoAsync(idPedido, modelo?.Estado);
        return resultado.Ok
            ? Json(new { estado = resultado.Mensaje })
            : BadRequest(resultado);
    }

    // ==================== ENVIAR SELECCIONADOS ====================
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
    public async Task<IActionResult> PreviewEnviarSeleccionados([FromBody] EnviarPedidosViewModel modelo)
    {
        var resultado = await _pedidoService.PreviewEnviarVariosAsync(modelo ?? new EnviarPedidosViewModel());
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }

    // ==================== ENVIAR (crear + enviar) ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> Enviar([FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.CrearAsync(modelo ?? new CrearPedidoViewModel(), enviar: true);
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> PreviewEnviar([FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.PreviewCrearAsync(modelo ?? new CrearPedidoViewModel());
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }
}