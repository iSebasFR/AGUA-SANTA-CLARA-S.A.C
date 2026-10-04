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
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        string? estado,
        long? idCliente,
        long? idRepartidor,
        long? idProducto)
    {
        var filtrosActivos = (fechaDesde.HasValue || fechaHasta.HasValue ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(estado) ? 1 : 0)
            + (idCliente.HasValue ? 1 : 0)
            + (idRepartidor.HasValue ? 1 : 0)
            + (idProducto.HasValue ? 1 : 0);
        var errorFiltros = filtrosActivos > 3
            ? "Puedes combinar como máximo 3 filtros."
            : fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde > fechaHasta
                ? "La fecha Desde no puede ser posterior a la fecha Hasta."
                : !string.IsNullOrWhiteSpace(estado) && !EstadosPedido.Todos.Contains(estado, StringComparer.Ordinal)
                    ? "Selecciona un estado válido."
                    : null;

        var consultaPedidos = _context.Pedidos.Where(p => p.EstadoRegistro);
        if (errorFiltros == null)
        {
            if (fechaDesde.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.FechaCreacion >= InicioDelDiaUtc(fechaDesde.Value));
            if (fechaHasta.HasValue && fechaHasta.Value < DateOnly.MaxValue)
                consultaPedidos = consultaPedidos.Where(p => p.FechaCreacion < InicioDelDiaUtc(fechaHasta.Value.AddDays(1)));
            if (!string.IsNullOrWhiteSpace(estado))
                consultaPedidos = consultaPedidos.Where(p => p.Estado == estado);
            if (idCliente.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.Clientes.Any(pc => pc.EstadoRegistro && pc.IdCliente == idCliente.Value));
            if (idRepartidor.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.IdRepartidor == idRepartidor.Value);
            if (idProducto.HasValue)
                consultaPedidos = consultaPedidos.Where(p => p.Clientes.Any(pc => pc.EstadoRegistro
                    && pc.Detalles.Any(d => d.EstadoRegistro && d.IdProducto == idProducto.Value)));
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
                .Include(p => p.Local)
                .Include(p => p.Repartidor)
                .Include(p => p.Clientes).ThenInclude(c => c.Cliente)
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
            ProductosFiltro = await _context.Productos
                .Where(p => p.EstadoRegistro)
                .OrderBy(p => p.Nombre)
                .ToListAsync(),
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            EstadoFiltro = estado,
            IdClienteFiltro = idCliente,
            IdRepartidorFiltro = idRepartidor,
            IdProductoFiltro = idProducto,
            ErrorFiltros = errorFiltros,
            TieneFiltrosAplicados = filtrosActivos > 0 && errorFiltros == null
        };

        return View(modelo);
    }

    private static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ObtenerParaEditar(long idPedido)
    {
        var pedido = await _context.Pedidos
            .Where(p => p.Id == idPedido && p.EstadoRegistro)
            .Select(p => new
            {
                idPedido = p.Id,
                idLocal = p.IdLocal,
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
    public async Task<IActionResult> BuscarCliente(string tipo, string q)
    {
        var cliente = await _pedidoService.BuscarClienteAsync(tipo, q);
        return cliente == null
            ? NotFound(new { mensaje = "No se encontró un cliente con esos criterios." })
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
    public async Task<IActionResult> CambiarEstado(long idPedido, [FromBody] ActualizarEstadoPedidoViewModel? modelo)
    {
        var resultado = await _pedidoService.CambiarEstadoAsync(idPedido, modelo?.Estado);
        return resultado.Ok ? Json(new { estado = modelo!.Estado }) : BadRequest(resultado);
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
    public async Task<IActionResult> PreviewEnviarSeleccionados([FromBody] EnviarPedidosViewModel modelo)
    {
        var resultado = await _pedidoService.PreviewEnviarVariosAsync(modelo ?? new EnviarPedidosViewModel());
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> PreviewEnviar([FromBody] CrearPedidoViewModel modelo)
    {
        var resultado = await _pedidoService.PreviewCrearAsync(modelo ?? new CrearPedidoViewModel());
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }
}