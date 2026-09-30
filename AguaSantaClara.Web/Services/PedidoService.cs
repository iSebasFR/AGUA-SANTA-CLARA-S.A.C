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

    public PedidoService(AppDbContext context)
    {
        _context = context;
    }

    public static bool CelularValido(string? celular) =>
        !string.IsNullOrEmpty(celular) && CelularRegex.IsMatch(celular);

    public async Task<PedidoResultado> CrearAsync(CrearPedidoViewModel modelo, bool enviar)
    {
        var errores = new List<ErrorPedido>();

        var local = await _context.Locales
            .FirstOrDefaultAsync(l => l.Id == modelo.IdLocal && l.Estado && l.EstadoRegistro);
        if (local == null)
            errores.Add(new ErrorPedido("idLocal", "Selecciona el local de despacho."));

        if (modelo.FechaEntrega == null)
            errores.Add(new ErrorPedido("fechaEntrega", "La fecha y hora de entrega son obligatorias."));

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
                .Include(p => p.Producto)
                .Where(p => p.IdLocal == local.Id
                    && idsProductos.Contains(p.IdProducto)
                    && p.Estado && p.EstadoRegistro
                    && p.Producto.Estado && p.Producto.EstadoRegistro)
                .ToDictionaryAsync(p => p.IdProducto);

        var pedido = new Pedido
        {
            IdLocal = modelo.IdLocal,
            IdRepartidor = repartidor?.Id,
            FechaEntrega = modelo.FechaEntrega ?? default,
            Estado = enviar ? EstadosPedido.Enviado : EstadosPedido.Pendiente,
            Local = local!
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
                    errores.Add(new ErrorPedido($"{campo}.idProducto", "Selecciona un producto disponible en el local."));
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

        foreach (var (idProducto, cantidad) in cantidadPorProducto)
        {
            var productoLocal = disponibles[idProducto];
            if (cantidad > productoLocal.Stock)
                errores.Add(new ErrorPedido(
                    $"stock:{idProducto}",
                    $"Stock insuficiente: {productoLocal.Producto.Nombre} (disponible: {productoLocal.Stock})"));
        }

        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        pedido.Total = pedido.Clientes.Sum(c => c.Subtotal);
        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();

        var resultado = new PedidoResultado { IdPedido = pedido.Id, Total = pedido.Total };
        if (enviar)
        {
            var mensaje = PedidoMensajeBuilder.Construir(pedido);
            resultado.WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(repartidor!.Celular, mensaje);
        }

        return resultado;
    }

    public async Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string termino)
    {
        var valor = termino?.Trim();
        if (string.IsNullOrEmpty(valor))
            return null;

        var cliente = await _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .Where(c => c.Estado && c.EstadoRegistro && (c.Telefono == valor || c.Dni == valor))
            .OrderBy(c => c.Nombre)
            .FirstOrDefaultAsync();

        if (cliente == null)
            return null;

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

    public Task<List<ProductoLocalViewModel>> ProductosPorLocalAsync(long idLocal) =>
        _context.ProductosLocal
            .Where(p => p.IdLocal == idLocal
                && p.Estado && p.EstadoRegistro
                && p.Producto.Estado && p.Producto.EstadoRegistro)
            .OrderBy(p => p.Producto.Nombre)
            .Select(p => new ProductoLocalViewModel
            {
                IdProducto = p.IdProducto,
                Nombre = p.Producto.Nombre,
                Precio = p.Producto.PrecioVenta,
                Stock = p.Stock
            })
            .ToListAsync();
}
