using System.Text.RegularExpressions;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AguaSantaClara.Web.Services;

// HU-14 T10: registro de autoventas en ruta
public class AutoventaService : IAutoventaService
{
    private static readonly Regex DniRegex = new(@"^\d{8}$", RegexOptions.Compiled);

    private const string MsgDniDuplicado = "Ya existe un cliente con ese dni";
    private const string MsgExito = "Autoventa registrada correctamente";
    private const string MsgExitoClienteNuevo = "Autoventa y cliente registrados correctamente";

    private readonly AppDbContext _context;

    public AutoventaService(AppDbContext context) => _context = context;

    public async Task<PedidoResultado> GuardarAsync(GuardarAutoventaViewModel modelo)
    {
        var errores = new List<ErrorPedido>();

        // Todo el trabajo (validar, recalcular y guardar) ocurre dentro de una sola transacción.
        // Si se sale sin Commit, se deshace todo.
        await using var transaccion = await _context.Database.BeginTransactionAsync();

        // ---------- CLIENTE ----------
        Cliente? cliente = null;
        DireccionCliente? direccion = null;
        var esClienteNuevo = false;

        var tieneExistente = modelo.IdCliente.HasValue;
        var tieneNuevo = modelo.ClienteNuevo != null;

        if (tieneExistente && tieneNuevo)
        {
            errores.Add(new ErrorPedido("cliente", "Elige un cliente existente o registra uno nuevo, no ambos."));
        }
        else if (!tieneExistente && !tieneNuevo)
        {
            errores.Add(new ErrorPedido("cliente", "Selecciona un cliente existente o registra uno nuevo."));
        }
        else if (tieneExistente)
        {
            cliente = await _context.Clientes
                .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
                .FirstOrDefaultAsync(c => c.Id == modelo.IdCliente!.Value && c.Estado && c.EstadoRegistro);

            if (cliente == null)
            {
                errores.Add(new ErrorPedido("idCliente", "El cliente no existe o está inactivo."));
            }
            else
            {
                // PedidoCliente exige una dirección: se usa la principal (o la primera activa)
                direccion = cliente.Direcciones
                    .OrderByDescending(d => d.Principal)
                    .ThenBy(d => d.Id)
                    .FirstOrDefault();

                if (direccion == null)
                    errores.Add(new ErrorPedido("idCliente", "El cliente no tiene una dirección activa registrada."));
            }
        }
        else
        {
            esClienteNuevo = true;
            (cliente, direccion) = await ValidarYCrearClienteNuevoAsync(modelo.ClienteNuevo!, errores);
        }

        // ---------- MÉTODO DE PAGO ----------
        MetodoPago? metodoPago = null;
        if (!modelo.IdMetodoPago.HasValue)
        {
            errores.Add(new ErrorPedido("idMetodoPago", "Selecciona un método de pago."));
        }
        else
        {
            metodoPago = await _context.MetodosPago
                .FirstOrDefaultAsync(m => m.Id == modelo.IdMetodoPago.Value && m.Estado && m.EstadoRegistro);

            if (metodoPago == null)
                errores.Add(new ErrorPedido("idMetodoPago", "El método de pago no está disponible."));
        }

        // ---------- PRODUCTOS ----------
        var detalles = new List<DetallePedido>();

        if (modelo.Detalles.Count == 0)
        {
            errores.Add(new ErrorPedido("detalles", "Agrega al menos un producto."));
        }
        else
        {
            var idsLocales = modelo.Detalles.Select(d => d.IdLocal).Distinct().ToList();
            var idsProductos = modelo.Detalles.Select(d => d.IdProducto).Distinct().ToList();

            var disponibles = await _context.ProductosLocal
                .Include(pl => pl.Producto)
                .Where(pl => idsLocales.Contains(pl.IdLocal)
                    && idsProductos.Contains(pl.IdProducto)
                    && pl.Estado && pl.EstadoRegistro
                    && pl.Producto.Estado && pl.Producto.EstadoRegistro)
                .ToDictionaryAsync(pl => (pl.IdLocal, pl.IdProducto));

            for (var i = 0; i < modelo.Detalles.Count; i++)
            {
                var detalle = modelo.Detalles[i];
                var campo = $"detalles[{i}]";

                if (!disponibles.TryGetValue((detalle.IdLocal, detalle.IdProducto), out var productoLocal))
                {
                    errores.Add(new ErrorPedido($"{campo}.idProducto",
                        "El producto no está disponible en el local seleccionado."));
                    continue;
                }

                if (detalle.Cantidad <= 0)
                {
                    errores.Add(new ErrorPedido($"{campo}.cantidad", "La cantidad debe ser mayor a cero."));
                    continue;
                }

                if (detalle.DescuentoMonto < 0)
                {
                    errores.Add(new ErrorPedido($"{campo}.descuentoMonto", "El descuento no puede ser negativo."));
                    continue;
                }

                if (decimal.Round(detalle.DescuentoMonto, 2) != detalle.DescuentoMonto)
                {
                    errores.Add(new ErrorPedido($"{campo}.descuentoMonto",
                        "El descuento puede tener como máximo dos decimales."));
                    continue;
                }

                // El precio sale de la base de datos, nunca del navegador
                var precio = productoLocal.Producto.PrecioVenta;
                var bruto = detalle.Cantidad * precio;

                if (detalle.DescuentoMonto > bruto)
                {
                    errores.Add(new ErrorPedido($"{campo}.descuentoMonto",
                        $"El descuento no puede superar el monto del producto ({bruto:0.00})."));
                    continue;
                }

                detalles.Add(new DetallePedido
                {
                    IdLocal = detalle.IdLocal,
                    IdProducto = detalle.IdProducto,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = precio,
                    DescuentoMonto = detalle.DescuentoMonto,
                    Subtotal = bruto - detalle.DescuentoMonto
                });
            }
        }

        // ---------- TOTAL (recalculado en el servidor) ----------
        var total = detalles.Sum(d => d.Subtotal);

        if (errores.Count == 0 && total <= 0)
            errores.Add(new ErrorPedido("total", "El total a cobrar debe ser mayor a cero."));

        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        // ---------- ARMAR Y GUARDAR ----------
        var pedidoCliente = new PedidoCliente
        {
            Cliente = cliente!,
            Direccion = direccion!,
            Subtotal = total
        };
        foreach (var detalle in detalles)
            pedidoCliente.Detalles.Add(detalle);

        var pedido = new Pedido
        {
            IdRepartidor = null, // el repartidor se asocia en la T12
            Estado = EstadosPedido.Pagado,
            EsAutoventa = true,
            Total = total
        };
        pedido.Clientes.Add(pedidoCliente);

        var pago = new Pago
        {
            Pedido = pedido,
            Cliente = cliente!,
            IdMetodoPago = metodoPago!.Id,
            Monto = total
        };

        _context.Pedidos.Add(pedido);
        _context.Pagos.Add(pago);

        try
        {
            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }
        catch (DbUpdateException ex) when (EsDniDuplicado(ex))
        {
            // Otro usuario registró el mismo DNI entre la validación y el guardado
            await transaccion.RollbackAsync();
            return new PedidoResultado
            {
                Errores = { new ErrorPedido("clienteNuevo.dni", MsgDniDuplicado) }
            };
        }

        return new PedidoResultado
        {
            IdPedido = pedido.Id,
            Total = pedido.Total,
            Mensaje = esClienteNuevo ? MsgExitoClienteNuevo : MsgExito
        };
    }

    // Valida los datos del cliente nuevo y, si son correctos, arma la entidad con su dirección principal
    private async Task<(Cliente?, DireccionCliente?)> ValidarYCrearClienteNuevoAsync(
        ClienteNuevoAutoventaViewModel datos, List<ErrorPedido> errores)
    {
        var nombre = datos.Nombre?.Trim();
        var telefono = datos.Telefono?.Trim();
        var dni = string.IsNullOrWhiteSpace(datos.Dni) ? null : datos.Dni.Trim();
        var textoDireccion = datos.Direccion?.Trim();
        var ciudad = string.IsNullOrWhiteSpace(datos.Ciudad) ? null : datos.Ciudad.Trim();
        var referencia = string.IsNullOrWhiteSpace(datos.Referencia) ? null : datos.Referencia.Trim();

        var erroresAntes = errores.Count;

        if (string.IsNullOrEmpty(nombre))
            errores.Add(new ErrorPedido("clienteNuevo.nombre", "Ingresa el nombre o la razón social."));
        else if (nombre.Length > 150)
            errores.Add(new ErrorPedido("clienteNuevo.nombre", "El nombre no puede superar los 150 caracteres."));

        if (string.IsNullOrEmpty(telefono))
            errores.Add(new ErrorPedido("clienteNuevo.telefono", "Ingresa el teléfono."));
        else if (telefono.Length > 20)
            errores.Add(new ErrorPedido("clienteNuevo.telefono", "El teléfono no puede superar los 20 caracteres."));

        if (string.IsNullOrEmpty(textoDireccion))
            errores.Add(new ErrorPedido("clienteNuevo.direccion", "Ingresa la dirección."));
        else if (textoDireccion.Length > 255)
            errores.Add(new ErrorPedido("clienteNuevo.direccion", "La dirección no puede superar los 255 caracteres."));

        if (ciudad != null && ciudad.Length > 100)
            errores.Add(new ErrorPedido("clienteNuevo.ciudad", "La ciudad no puede superar los 100 caracteres."));

        if (referencia != null && referencia.Length > 255)
            errores.Add(new ErrorPedido("clienteNuevo.referencia", "La referencia no puede superar los 255 caracteres."));

        if (dni != null)
        {
            if (!DniRegex.IsMatch(dni))
            {
                errores.Add(new ErrorPedido("clienteNuevo.dni", "El DNI debe tener 8 dígitos."));
            }
            else if (await _context.Clientes.AnyAsync(c => c.Dni == dni))
            {
                // Misma regla que el índice único uq_cliente_dni: no filtra por EstadoRegistro
                errores.Add(new ErrorPedido("clienteNuevo.dni", MsgDniDuplicado));
            }
        }

        if (errores.Count > erroresAntes)
            return (null, null);

        var direccion = new DireccionCliente
        {
            Direccion = textoDireccion!,
            Ciudad = ciudad,
            Referencia = referencia,
            Principal = true
        };

        var cliente = new Cliente
        {
            Nombre = nombre!,
            Telefono = telefono!,
            Dni = dni
        };
        cliente.Direcciones.Add(direccion);

        return (cliente, direccion);
    }

    // true si la excepción viene de violar el índice único del DNI
    private static bool EsDniDuplicado(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_cliente_dni"
        };
}