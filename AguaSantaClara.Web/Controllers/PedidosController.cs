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
        // HU-14 T13: "Autoventa" es un filtro adicional, no un estado real de EstadosPedido.
        // Se separa aquí para que el resto del método (validación y filtro por Estado) lo ignore.
        var esFiltroAutoventa = string.Equals(estado?.Trim(), "Autoventa", StringComparison.OrdinalIgnoreCase);
        if (esFiltroAutoventa) estado = null;

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

        // HU-14 T13: solo las autoventas en ruta (se filtra por EsAutoventa, no por Estado: están "Pagado")
        if (esFiltroAutoventa)
            consultaPedidos = consultaPedidos.Where(p => p.EsAutoventa);

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

        var incidencias = await _context.Incidencias
            .Where(i => i.EstadoRegistro)
            .OrderByDescending(i => i.FechaCreacion)
            .ToListAsync();

        var motivosIncidenciaPorPedido = incidencias
            .GroupBy(i => i.IdPedido)
            .ToDictionary(
                grupo => grupo.Key,
                grupo =>
                {
                    var incidencia = grupo.First();

                    return incidencia.Motivo == MotivosIncidencia.Otros &&
                        !string.IsNullOrWhiteSpace(incidencia.Detalle)
                        ? incidencia.Detalle!
                        : incidencia.Motivo;
                });

        var modelo = new PedidosIndexViewModel
        {
            MotivosIncidenciaPorPedido = motivosIncidenciaPorPedido,
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
            MetodosPago = await _context.MetodosPago
                .Where(m => m.Estado && m.EstadoRegistro)
                .OrderBy(m => m.Nombre)
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

        // HU-14 T13: el filtro "Autoventa" se muestra seleccionado y cuenta como filtro aplicado
        if (esFiltroAutoventa)
        {
            modelo.EstadoFiltro = "Autoventa";
            modelo.TieneFiltrosAplicados = true;
        }

        // HU-14 T13: resumen de ventas de hoy (sin pedidos Pendiente ni Con Incidencia)
        var inicioHoyUtc = InicioDelDiaUtc(DateOnly.FromDateTime(DateTime.Now));
        var finHoyUtc = inicioHoyUtc.AddDays(1);

        var ventasHoy = await _context.Pedidos
            .Where(p => p.EstadoRegistro
                && p.FechaCreacion >= inicioHoyUtc && p.FechaCreacion < finHoyUtc
                && p.Estado != EstadosPedido.Pendiente && p.Estado != EstadosPedido.ConIncidencia)
            .GroupBy(p => p.EsAutoventa)
            .Select(g => new { EsAutoventa = g.Key, Cantidad = g.Count(), Monto = g.Sum(p => p.Total) })
            .ToListAsync();

        var normalesHoy = ventasHoy.FirstOrDefault(v => !v.EsAutoventa);
        var autoventasHoy = ventasHoy.FirstOrDefault(v => v.EsAutoventa);
        modelo.PedidosNormalesHoy = normalesHoy?.Cantidad ?? 0;
        modelo.MontoPedidosNormalesHoy = normalesHoy?.Monto ?? 0m;
        modelo.AutoventasHoy = autoventasHoy?.Cantidad ?? 0;
        modelo.MontoAutoventasHoy = autoventasHoy?.Monto ?? 0m;

        var cobradoAutoventas = await _context.Pagos
            .Where(pg => pg.EstadoRegistro
                && pg.Pedido.EstadoRegistro && pg.Pedido.EsAutoventa
                && pg.Pedido.FechaCreacion >= inicioHoyUtc && pg.Pedido.FechaCreacion < finHoyUtc)
            .GroupBy(pg => pg.IdMetodoPago)
            .Select(g => new { IdMetodoPago = g.Key, Monto = g.Sum(pg => pg.Monto) })
            .ToListAsync();

        var nombresMetodos = await _context.MetodosPago.ToDictionaryAsync(m => m.Id, m => m.Nombre);
        modelo.CobradoAutoventasPorMetodoHoy = cobradoAutoventas
            .GroupBy(c => nombresMetodos.GetValueOrDefault(c.IdMetodoPago, "Otro"))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Monto));

        var idsPedidos = modelo.Pedidos
            .Where(p => p.Estado is EstadosPedido.Entregado or EstadosPedido.PagoParcial)
            .Select(p => p.Id)
            .ToList();
        var pagosExistentes = await _context.Pagos
            .Where(p => p.EstadoRegistro && idsPedidos.Contains(p.IdPedido))
            .GroupBy(p => new { p.IdPedido, p.IdCliente })
            .Select(grupo => new
            {
                grupo.Key.IdPedido,
                grupo.Key.IdCliente,
                Monto = grupo.Sum(p => p.Monto)
            })
            .ToListAsync();
        modelo.MontosPagadosPorPedidoCliente = pagosExistentes
            .GroupBy(grupo => grupo.IdPedido)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.ToDictionary(p => p.IdCliente, p => p.Monto));

        var deudasExistentes = await _context.Deudas
            .Where(deuda => deuda.EstadoRegistro && idsPedidos.Contains(deuda.IdPedido))
            .Select(deuda => new
            {
                deuda.Id,
                deuda.IdPedido,
                deuda.IdCliente,
                deuda.Monto,
                deuda.MontoPagadoAlRegistrar,
                deuda.FechaVencimiento,
                deuda.Estado
            })
            .ToListAsync();
        modelo.DeudasPorPedidoCliente = deudasExistentes
            .GroupBy(deuda => deuda.IdPedido)
            .ToDictionary(
                grupoPedido => grupoPedido.Key,
                grupoPedido => grupoPedido
                    .GroupBy(deuda => deuda.IdCliente)
                    .ToDictionary(
                        grupoCliente => grupoCliente.Key,
                        grupoCliente =>
                        {
                            var deuda = grupoCliente.OrderByDescending(item => item.Id).First();
                            return new DeudaPedidoResumen(deuda.Monto, deuda.MontoPagadoAlRegistrar, deuda.FechaVencimiento, deuda.Estado);
                        }));

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

    // ==================== VALIDAR DNI DE CLIENTE NUEVO (HU-14 T04) ====================
    [HttpGet]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ValidarDniCliente(string? dni)
    {
        var valor = dni?.Trim();
        if (string.IsNullOrEmpty(valor))
            return Json(new { existe = false });

        var existe = await _context.Clientes.AnyAsync(c => c.Dni == valor);
        return Json(new { existe });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> RegistrarPagos(
        long idPedido,
        [FromBody] RegistrarPagosPedidoViewModel? modelo)
    {
        var resultado = await _pedidoService.RegistrarPagosAsync(
            idPedido,
            modelo ?? new RegistrarPagosPedidoViewModel());

        return resultado.Ok
            ? Json(new { mensaje = resultado.Mensaje })
            : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> RegistrarDeuda(
        long idPedido,
        [FromBody] RegistrarDeudaViewModel? modelo)
    {
        var resultado = await _pedidoService.RegistrarDeudaAsync(
            idPedido,
            modelo ?? new RegistrarDeudaViewModel());

        return resultado.Ok
            ? Json(new { mensaje = resultado.Mensaje })
            : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> AutorizarPlazo(
        long idPedido,
        [FromBody] AutorizarPlazoViewModel? modelo)
    {
        var idUsuarioTexto = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!long.TryParse(idUsuarioTexto, out var idUsuario))
            return Unauthorized();

        var resultado = await _pedidoService.AutorizarPlazoAsync(
            idPedido,
            modelo ?? new AutorizarPlazoViewModel(),
            idUsuario);

        return resultado.Ok
            ? Json(new { mensaje = resultado.Mensaje })
            : BadRequest(resultado);
    }

    // ==================== REGISTRAR INCIDENCIA ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> RegistrarIncidencia(
        long idPedido,
        [FromBody] RegistrarIncidenciaViewModel? modelo)
    {
        var idUsuarioTexto = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!long.TryParse(idUsuarioTexto, out var idUsuario))
            return Unauthorized();

        var resultado = await _pedidoService.RegistrarIncidenciaAsync(
            idPedido,
            modelo ?? new RegistrarIncidenciaViewModel(),
            idUsuario);

        return resultado.Ok
            ? Json(resultado)
            : BadRequest(resultado);
    }

    // ==================== REINTENTAR ENTREGA ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> PreviewReintentarEntrega(
        long idPedido,
        [FromBody] ReintentarEntregaViewModel? modelo)
    {
        var resultado =
            await _pedidoService.PreviewReintentarEntregaAsync(
                idPedido,
                modelo ?? new ReintentarEntregaViewModel());

        return resultado.Ok
            ? Json(resultado)
            : BadRequest(resultado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> ReintentarEntrega(
        long idPedido,
        [FromBody] ReintentarEntregaViewModel? modelo)
    {
        var resultado =
            await _pedidoService.ReintentarEntregaAsync(
                idPedido,
                modelo ?? new ReintentarEntregaViewModel());

        return resultado.Ok
            ? Json(resultado)
            : BadRequest(resultado);
    }

    // ==================== CANCELAR PEDIDO CON INCIDENCIA ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> CancelarConIncidencia(long idPedido)
    {
        var resultado =
            await _pedidoService.CancelarConIncidenciaAsync(idPedido);

        return resultado.Ok
            ? Json(resultado)
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

    // ==================== GUARDAR AUTOVENTA (HU-14 T10) ====================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Vendedora")]
    public async Task<IActionResult> GuardarAutoventa(
        [FromBody] GuardarAutoventaViewModel? modelo,
        [FromServices] IAutoventaService autoventaService)
    {
        var resultado = await autoventaService.GuardarAsync(modelo ?? new GuardarAutoventaViewModel());
        return resultado.Ok ? Json(resultado) : BadRequest(resultado);
    }
}