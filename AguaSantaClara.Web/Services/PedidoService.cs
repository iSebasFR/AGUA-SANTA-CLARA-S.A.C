using System.Text.RegularExpressions;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

public class PedidoService : IPedidoService
{
    private static readonly Regex CelularRegex = new(@"^519\d{8}$", RegexOptions.Compiled);
    private readonly AppDbContext _context;

    public PedidoService(AppDbContext context) => _context = context;

    public static bool CelularValido(string? celular) =>
        !string.IsNullOrEmpty(celular) && CelularRegex.IsMatch(celular);

    public Task<PedidoResultado> CrearAsync(CrearPedidoViewModel modelo, bool enviar) =>
        GuardarAsync(modelo, enviar, null, persistir: true);

    public Task<PedidoResultado> PreviewCrearAsync(CrearPedidoViewModel modelo) =>
        GuardarAsync(modelo, enviar: true, null, persistir: false);

    public Task<PedidoResultado> ActualizarAsync(long idPedido, CrearPedidoViewModel modelo) =>
        GuardarAsync(modelo, enviar: false, idPedido, persistir: true);

    public async Task<bool> EliminarAsync(long idPedido)
    {
        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);
        if (pedido == null) return false;

        pedido.EstadoRegistro = false;
        pedido.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PedidoResultado> CambiarEstadoAsync(long idPedido, string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado) || !EstadosPedido.Todos.Contains(estado, StringComparer.Ordinal))
            return new PedidoResultado { Errores = { new ErrorPedido("estado", "Selecciona un estado válido.") } };

        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);
        if (pedido == null)
            return new PedidoResultado { Errores = { new ErrorPedido("pedido", "El pedido no existe.") } };

        pedido.Estado = estado;
        pedido.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return new PedidoResultado();
    }

    private async Task<PedidoResultado> GuardarAsync(CrearPedidoViewModel modelo, bool enviar, long? idPedido, bool persistir)
    {
        var errores = new List<ErrorPedido>();
        Pedido? pedidoExistente = null;

        Local? local = null;
        if (modelo.IdLocal != null)
            local = await _context.Locales
                .FirstOrDefaultAsync(l => l.Id == modelo.IdLocal && l.Estado && l.EstadoRegistro);

        if (local == null)
            errores.Add(new ErrorPedido("idLocal", "Selecciona el local de despacho."));

        if (idPedido.HasValue)
        {
            pedidoExistente = await _context.Pedidos
                .Include(p => p.Clientes).ThenInclude(c => c.Detalles)
                .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);
            if (pedidoExistente == null)
                return new PedidoResultado { Errores = { new ErrorPedido("pedido", "El pedido no existe.") } };
        }

        if (modelo.Clientes.Count == 0)
            errores.Add(new ErrorPedido("clientes", "Agrega al menos un cliente."));

        Repartidor? repartidor = null;
        if (enviar)
        {
            if (modelo.IdRepartidor != null)
                repartidor = await _context.Repartidores
                    .FirstOrDefaultAsync(r => r.Id == modelo.IdRepartidor && r.Estado && r.EstadoRegistro);

            if (repartidor == null)
                errores.Add(new ErrorPedido("idRepartidor", "Selecciona un repartidor."));
            else if (!CelularValido(repartidor.Celular))
                errores.Add(new ErrorPedido("idRepartidor", "El celular del repartidor no tiene un formato válido."));
        }

        var idsClientes = modelo.Clientes.Select(c => c.IdCliente).Distinct().ToList();
        var idsProductos = modelo.Clientes.SelectMany(c => c.Detalles).Select(d => d.IdProducto).Distinct().ToList();

        var clientes = await _context.Clientes
            .Include(c => c.Direcciones)
            .Where(c => idsClientes.Contains(c.Id) && c.Estado && c.EstadoRegistro)
            .ToDictionaryAsync(c => c.Id);

        var disponibles = local == null
            ? new Dictionary<long, ProductoLocal>()
            : await _context.ProductosLocal
                .Include(pl => pl.Producto)
                .Where(pl => pl.IdLocal == local.Id
                    && idsProductos.Contains(pl.IdProducto)
                    && pl.Estado && pl.EstadoRegistro
                    && pl.Producto.Estado && pl.Producto.EstadoRegistro)
                .ToDictionaryAsync(pl => pl.IdProducto);

        var pedido = new Pedido
        {
            IdLocal = local?.Id,
            IdRepartidor = repartidor?.Id,
            Estado = enviar ? EstadosPedido.Enviado : EstadosPedido.Pendiente
        };

        var paresUsados = new HashSet<(long, long)>();
        var cantidadPorProducto = new Dictionary<long, int>();

        for (var i = 0; i < modelo.Clientes.Count; i++)
        {
            var linea = modelo.Clientes[i];
            var prefijo = $"clientes[{i}]";

            if (!clientes.TryGetValue(linea.IdCliente, out var cliente))
            {
                errores.Add(new ErrorPedido($"{prefijo}.idCliente", "El cliente no existe o está inactivo."));
                continue;
            }

            var direccion = cliente.Direcciones.FirstOrDefault(d => d.Id == linea.IdDireccion && d.EstadoRegistro);
            if (direccion == null)
                errores.Add(new ErrorPedido($"{prefijo}.idDireccion", "Selecciona una dirección de entrega del cliente."));
            else if (!paresUsados.Add((cliente.Id, direccion.Id)))
                errores.Add(new ErrorPedido($"{prefijo}.idDireccion", "Esa dirección ya está agregada para este cliente."));

            if (linea.Detalles.Count == 0)
                errores.Add(new ErrorPedido($"{prefijo}.detalles", "Agrega al menos un producto."));

            var pedidoCliente = new PedidoCliente
            {
                IdCliente = cliente.Id,
                Cliente = cliente,
                IdDireccion = direccion?.Id ?? 0,
                Direccion = direccion!
            };

            for (var j = 0; j < linea.Detalles.Count; j++)
            {
                var detalle = linea.Detalles[j];
                var campo = $"{prefijo}.detalles[{j}]";

                if (!disponibles.TryGetValue(detalle.IdProducto, out var productoLocal))
                {
                    errores.Add(new ErrorPedido($"{campo}.idProducto", "El producto no está disponible en este local."));
                    continue;
                }

                if (detalle.Cantidad <= 0)
                {
                    errores.Add(new ErrorPedido($"{campo}.cantidad", "La cantidad debe ser mayor a cero."));
                    continue;
                }

                cantidadPorProducto[detalle.IdProducto] =
                    cantidadPorProducto.GetValueOrDefault(detalle.IdProducto) + detalle.Cantidad;

                var precio = productoLocal.Producto.PrecioVenta;
                pedidoCliente.Detalles.Add(new DetallePedido
                {
                    IdProducto = detalle.IdProducto,
                    Producto = productoLocal.Producto,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = precio,
                    Subtotal = detalle.Cantidad * precio
                });
            }

            pedidoCliente.Subtotal = pedidoCliente.Detalles.Sum(d => d.Subtotal);
            pedido.Clientes.Add(pedidoCliente);
        }

        if (local != null)
        {
            foreach (var (idProducto, cantidad) in cantidadPorProducto)
            {
                var productoLocal = disponibles[idProducto];
                if (cantidad > productoLocal.Stock)
                    errores.Add(new ErrorPedido(
                        $"stock:{idProducto}",
                        $"Stock insuficiente en {local.Nombre}: {productoLocal.Producto.Nombre} (disponible: {productoLocal.Stock})"));
            }
        }

        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        pedido.Total = pedido.Clientes.Sum(c => c.Subtotal);

        if (pedidoExistente == null)
        {
            if (persistir) _context.Pedidos.Add(pedido);
        }
        else
        {
            if (persistir)
            {
                _context.RemoveRange(pedidoExistente.Clientes.SelectMany(c => c.Detalles));
                _context.RemoveRange(pedidoExistente.Clientes);
                pedidoExistente.Clientes.Clear();
                pedidoExistente.IdLocal = local?.Id;
                pedidoExistente.IdRepartidor = modelo.IdRepartidor;
                pedidoExistente.Total = pedido.Total;
                pedidoExistente.FechaActualizacion = DateTime.UtcNow;
                foreach (var cliente in pedido.Clientes)
                    pedidoExistente.Clientes.Add(cliente);
            }
            pedido = pedidoExistente;
        }

        if (persistir)
            await _context.SaveChangesAsync();

        var resultado = new PedidoResultado { IdPedido = pedido.Id, Total = pedido.Total };
        if (enviar)
        {
            if (pedido.Local == null && local != null)
                pedido.Local = local;

            var mensaje = PedidoMensajeBuilder.Construir(pedido);
            resultado.Mensaje = mensaje;
            resultado.WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(repartidor!.Celular, mensaje);
        }

        return resultado;
    }

    public async Task<PedidoResultado> EnviarVariosAsync(EnviarPedidosViewModel modelo)
    {
        var (errores, pedidos, repartidor) = await ValidarEnvioVariosAsync(modelo);
        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        foreach (var pedido in pedidos!)
        {
            pedido.IdRepartidor = repartidor!.Id;
            if (pedido.Estado == EstadosPedido.Pendiente)
                pedido.Estado = EstadosPedido.Enviado;
            pedido.FechaActualizacion = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        var mensaje = PedidoMensajeBuilder.ConstruirVarios(pedidos!);
        return new PedidoResultado
        {
            Total = pedidos!.Sum(p => p.Total),
            Mensaje = mensaje,
            WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(repartidor!.Celular, mensaje)
        };
    }

    public async Task<PedidoResultado> PreviewEnviarVariosAsync(EnviarPedidosViewModel modelo)
    {
        var (errores, pedidos, repartidor) = await ValidarEnvioVariosAsync(modelo);
        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        var mensaje = PedidoMensajeBuilder.ConstruirVarios(pedidos!);
        return new PedidoResultado
        {
            Total = pedidos!.Sum(p => p.Total),
            Mensaje = mensaje,
            WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(repartidor!.Celular, mensaje)
        };
    }

    private async Task<(List<ErrorPedido>, List<Pedido>?, Repartidor?)> ValidarEnvioVariosAsync(EnviarPedidosViewModel modelo)
    {
        var errores = new List<ErrorPedido>();
        var ids = modelo.IdsPedidos.Distinct().ToList();

        if (ids.Count == 0)
            errores.Add(new ErrorPedido("pedidos", "Selecciona al menos un pedido."));

        Repartidor? repartidor = null;
        if (modelo.IdRepartidor != null)
            repartidor = await _context.Repartidores
                .FirstOrDefaultAsync(r => r.Id == modelo.IdRepartidor && r.Estado && r.EstadoRegistro);

        if (repartidor == null)
            errores.Add(new ErrorPedido("idRepartidor", "Selecciona un repartidor."));
        else if (!CelularValido(repartidor.Celular))
            errores.Add(new ErrorPedido("idRepartidor", "El celular del repartidor no tiene un formato válido."));

        var pedidos = await _context.Pedidos
            .Include(p => p.Local)
            .Include(p => p.Clientes).ThenInclude(c => c.Cliente)
            .Include(p => p.Clientes).ThenInclude(c => c.Direccion)
            .Include(p => p.Clientes).ThenInclude(c => c.Detalles).ThenInclude(d => d.Producto)
            .Where(p => ids.Contains(p.Id) && p.EstadoRegistro)
            .ToListAsync();

        if (pedidos.Count != ids.Count)
            errores.Add(new ErrorPedido("pedidos", "Algún pedido seleccionado no existe."));

        return (errores, pedidos, repartidor);
    }

    public async Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string tipo, string termino)
    {
        var valor = termino?.Trim();
        if (string.IsNullOrEmpty(valor)) return null;

        var query = _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .Where(c => c.Estado && c.EstadoRegistro);

        query = tipo?.ToLowerInvariant() switch
        {
            "telefono" => query.Where(c => EF.Functions.ILike(c.Telefono, $"%{valor}%")),
            "dni" => query.Where(c => c.Dni != null && EF.Functions.ILike(c.Dni, $"%{valor}%")),
            "nombre" => query.Where(c => EF.Functions.ILike(c.Nombre, $"%{valor}%")),
            _ => query.Where(c =>
                EF.Functions.ILike(c.Telefono, $"%{valor}%") ||
                (c.Dni != null && EF.Functions.ILike(c.Dni, $"%{valor}%")) ||
                EF.Functions.ILike(c.Nombre, $"%{valor}%"))
        };

        var cliente = await query
            .OrderBy(c => c.Nombre)
            .FirstOrDefaultAsync();

        if (cliente == null) return null;

        return new ClienteBusquedaViewModel
        {
            Id = cliente.Id,
            Nombre = cliente.Nombre,
            Telefono = cliente.Telefono,
            Dni = cliente.Dni,
            Direcciones = cliente.Direcciones
                .OrderByDescending(d => d.Principal)
                .ThenBy(d => d.Id)
                .Select(d => new DireccionBusquedaViewModel
                {
                    Id = d.Id,
                    Direccion = d.Direccion,
                    Ciudad = d.Ciudad,
                    UrlUbicacion = d.UrlUbicacion,
                    Principal = d.Principal
                })
                .ToList()
        };
    }

    public Task<List<ProductoPedidoViewModel>> ProductosPorLocalAsync(long idLocal) =>
        _context.ProductosLocal
            .Where(pl => pl.IdLocal == idLocal
                && pl.Estado && pl.EstadoRegistro
                && pl.Producto.Estado && pl.Producto.EstadoRegistro)
            .OrderBy(pl => pl.Producto.Nombre)
            .Select(pl => new ProductoPedidoViewModel
            {
                IdProducto = pl.IdProducto,
                Nombre = pl.Producto.Nombre,
                Precio = pl.Producto.PrecioVenta,
                Stock = pl.Stock
            })
            .ToListAsync();
}