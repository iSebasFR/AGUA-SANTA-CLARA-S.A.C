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
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PedidoResultado> CambiarEstadoAsync(long idPedido, string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado) ||
            !EstadosPedido.Todos.Contains(estado, StringComparer.OrdinalIgnoreCase))
        {
            return new PedidoResultado
            {
                Errores = { new ErrorPedido("estado", "Selecciona un estado válido.") }
            };
        }

        var estadoCanonico = EstadosPedido.Todos
            .First(e => string.Equals(e, estado, StringComparison.OrdinalIgnoreCase));

        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);

        if (pedido == null)
            return new PedidoResultado
            {
                Errores = { new ErrorPedido("pedido", "El pedido no existe.") }
            };

        var permitidos = EstadosPedido.SiguientesPermitidos(pedido.Estado).ToList();
        if (!permitidos.Contains(estadoCanonico, StringComparer.OrdinalIgnoreCase))
        {
            return new PedidoResultado
            {
                Errores =
                {
                    new ErrorPedido("estado",
                        permitidos.Count == 0
                            ? $"El pedido ya está en estado {pedido.Estado} y no admite más cambios."
                            : $"Solo puedes pasar de {pedido.Estado} a: {string.Join(", ", permitidos)}.")
                }
            };
        }

        pedido.Estado = estadoCanonico;
        await _context.SaveChangesAsync();
        return new PedidoResultado { Mensaje = estadoCanonico };
    }

    public async Task<PedidoResultado> RegistrarPagosAsync(
        long idPedido,
        RegistrarPagosPedidoViewModel modelo)
    {
        var resultado = new PedidoResultado { IdPedido = idPedido };
        if (modelo.Pagos.Count == 0)
        {
            resultado.Errores.Add(new ErrorPedido("pagos", "Ingresa al menos un pago."));
            return resultado;
        }

        var pedido = await _context.Pedidos
            .Include(p => p.Clientes.Where(pc => pc.EstadoRegistro))
            .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);

        if (pedido == null)
        {
            resultado.Errores.Add(new ErrorPedido("pedido", "El pedido no existe."));
            return resultado;
        }

        if (pedido.Estado is not (EstadosPedido.Entregado or EstadosPedido.PagoParcial))
        {
            resultado.Errores.Add(new ErrorPedido("pedido", "Solo se pueden registrar pagos de pedidos entregados o con pago parcial."));
            return resultado;
        }

        var montosPorCliente = pedido.Clientes
            .GroupBy(pc => pc.IdCliente)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(pc => pc.Subtotal));
        var idsMetodoPago = modelo.Pagos.Select(p => p.IdMetodoPago).Distinct().ToList();
        var metodosPagoActivos = await _context.MetodosPago
            .Where(m => idsMetodoPago.Contains(m.Id) && m.Estado && m.EstadoRegistro)
            .Select(m => m.Id)
            .ToListAsync();
        var metodosPagoActivosSet = metodosPagoActivos.ToHashSet();
        var montosPagados = await _context.Pagos
            .Where(p => p.IdPedido == idPedido && p.EstadoRegistro)
            .GroupBy(p => p.IdCliente)
            .Select(grupo => new { IdCliente = grupo.Key, Monto = grupo.Sum(p => p.Monto) })
            .ToDictionaryAsync(item => item.IdCliente, item => item.Monto);

        var montosNuevosPorCliente = modelo.Pagos
            .GroupBy(p => p.IdCliente)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(p => p.Monto));

        for (var indice = 0; indice < modelo.Pagos.Count; indice++)
        {
            var pago = modelo.Pagos[indice];
            var campo = $"pagos[{indice}]";

            if (!montosPorCliente.TryGetValue(pago.IdCliente, out var montoPedido))
            {
                resultado.Errores.Add(new ErrorPedido($"{campo}.idCliente", "El cliente no pertenece al pedido."));
                continue;
            }

            if (!metodosPagoActivosSet.Contains(pago.IdMetodoPago))
            {
                resultado.Errores.Add(new ErrorPedido($"{campo}.idMetodoPago", "Selecciona un método de pago activo."));
            }

            if (pago.Monto <= 0 || decimal.Round(pago.Monto, 2) != pago.Monto)
            {
                resultado.Errores.Add(new ErrorPedido($"{campo}.monto", "El monto debe ser mayor que cero y tener como máximo dos decimales."));
                continue;
            }

        }

        foreach (var (idCliente, montoNuevo) in montosNuevosPorCliente)
        {
            if (!montosPorCliente.TryGetValue(idCliente, out var montoPedido))
                continue;

            var montoPendiente = montoPedido - montosPagados.GetValueOrDefault(idCliente);
            if (montoNuevo > montoPendiente)
            {
                resultado.Errores.Add(new ErrorPedido(
                    "pagos",
                    $"El total registrado para el cliente supera su saldo pendiente ({montoPendiente:0.00})."));
            }
        }

        if (!resultado.Ok)
            return resultado;

        var clientes = await _context.Clientes
            .Where(c => montosPorCliente.Keys.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        var deudasExistentes = await _context.Deudas
            .Where(d => d.EstadoRegistro && d.IdPedido == idPedido)
            .ToListAsync();
        var deudaPorCliente = deudasExistentes
            .GroupBy(d => d.IdCliente)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

        foreach (var (idCliente, montoPedido) in montosPorCliente)
        {
            if (deudaPorCliente.ContainsKey(idCliente))
                continue;

            var deuda = new Deuda
            {
                IdPedido = idPedido,
                IdCliente = idCliente,
                Monto = montoPedido,
                FechaVencimiento = DateTime.UtcNow.Date,
                Estado = EstadosDeuda.Pendiente
            };

            _context.Deudas.Add(deuda);
            deudaPorCliente.Add(idCliente, deuda);
            clientes[idCliente].DeudaTotal += montoPedido;
        }

        foreach (var pago in modelo.Pagos)
            clientes[pago.IdCliente].DeudaTotal = Math.Max(
                0m,
                clientes[pago.IdCliente].DeudaTotal - pago.Monto);

        foreach (var (idCliente, deuda) in deudaPorCliente)
        {
            var montoPagadoDespuesDelRegistro =
                montosPagados.GetValueOrDefault(idCliente) +
                modelo.Pagos.Where(p => p.IdCliente == idCliente).Sum(p => p.Monto);
            if (montoPagadoDespuesDelRegistro >= deuda.Monto)
                deuda.Estado = EstadosDeuda.Pagada;
        }

        var montosPagadosDespuesDelRegistro = montosPagados.ToDictionary(
            pago => pago.Key,
            pago => pago.Value);
        foreach (var pago in modelo.Pagos)
        {
            montosPagadosDespuesDelRegistro[pago.IdCliente] =
                montosPagadosDespuesDelRegistro.GetValueOrDefault(pago.IdCliente) + pago.Monto;
        }

        pedido.Estado = montosPorCliente.Keys.All(idCliente =>
            montosPagadosDespuesDelRegistro.GetValueOrDefault(idCliente) >= montosPorCliente[idCliente])
            ? EstadosPedido.Pagado
            : EstadosPedido.PagoParcial;

        _context.Pagos.AddRange(modelo.Pagos.Select(pago => new Pago
        {
            IdPedido = idPedido,
            IdCliente = pago.IdCliente,
            IdMetodoPago = pago.IdMetodoPago,
            Monto = pago.Monto
        }));

        await _context.SaveChangesAsync();
        resultado.Mensaje = "Pagos registrados correctamente.";
        return resultado;
    }

    public async Task<PedidoResultado> RegistrarIncidenciaAsync(long idPedido,RegistrarIncidenciaViewModel modelo,long idUsuario)
    {
        var resultado = new PedidoResultado
        {
            IdPedido = idPedido
        };

        var motivo = modelo.Motivo?.Trim();
        var detalle = modelo.Detalle?.Trim();

        if (string.IsNullOrWhiteSpace(motivo) ||
            !MotivosIncidencia.Todos.Contains(
                motivo,
                StringComparer.OrdinalIgnoreCase))
        {
            resultado.Errores.Add(
                new ErrorPedido("motivo", "Selecciona un motivo de incidencia válido."));
            return resultado;
        }

        var motivoCanonico = MotivosIncidencia.Todos
            .First(m => string.Equals(
                m,
                motivo,
                StringComparison.OrdinalIgnoreCase));

        if (motivoCanonico == MotivosIncidencia.Otros &&
            string.IsNullOrWhiteSpace(detalle))
        {
            resultado.Errores.Add(
                new ErrorPedido("detalle", "Ingresa el motivo de la incidencia."));
            return resultado;
        }

        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p =>
                p.Id == idPedido &&
                p.EstadoRegistro);

        if (pedido == null)
        {
            resultado.Errores.Add(
                new ErrorPedido("pedido", "El pedido no existe."));
            return resultado;
        }

        if (pedido.Estado != EstadosPedido.Enviado)
        {
            resultado.Errores.Add(
                new ErrorPedido(
                    "pedido",
                    "Solo se puede registrar una incidencia en un pedido enviado."));
            return resultado;
        }

        var incidencia = new Incidencia
        {
            IdPedido = pedido.Id,
            IdUsuarioReporta = idUsuario,
            Motivo = motivoCanonico,
            Detalle = motivoCanonico == MotivosIncidencia.Otros
                ? detalle
                : null
        };

        _context.Incidencias.Add(incidencia);

        pedido.Estado = EstadosPedido.ConIncidencia;
        pedido.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        resultado.Mensaje = "INCIDENCIA REGISTRADA CORRECTAMENTE";

        return resultado;
    }

    private async Task<PedidoResultado> GuardarAsync(
        CrearPedidoViewModel modelo, bool enviar, long? idPedido, bool persistir)
    {
        var errores = new List<ErrorPedido>();
        Pedido? pedidoExistente = null;

        if (idPedido.HasValue)
        {
            pedidoExistente = await _context.Pedidos
                .Include(p => p.Clientes).ThenInclude(c => c.Detalles)
                .FirstOrDefaultAsync(p => p.Id == idPedido && p.EstadoRegistro);

            if (pedidoExistente == null)
                return new PedidoResultado
                {
                    Errores = { new ErrorPedido("pedido", "El pedido no existe.") }
                };

            if (pedidoExistente.Estado != EstadosPedido.Pendiente)
                return new PedidoResultado
                {
                    Errores = { new ErrorPedido("pedido", "Solo se pueden editar pedidos en estado Pendiente.") }
                };
        }

        if (modelo.Clientes.Count == 0)
            errores.Add(new ErrorPedido("clientes", "Agrega al menos un cliente."));

        // ✅ Regla: 1 cliente por pedido
        var idsClientesDistintos = modelo.Clientes.Select(c => c.IdCliente).Distinct().ToList();
        if (idsClientesDistintos.Count > 1)
            errores.Add(new ErrorPedido("clientes", "Solo se permite un cliente por pedido."));

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
        var idsLocales = modelo.Clientes
            .SelectMany(c => c.Detalles)
            .Select(d => d.IdLocal)
            .Distinct()
            .ToList();
        var idsProductos = modelo.Clientes
            .SelectMany(c => c.Detalles)
            .Select(d => d.IdProducto)
            .Distinct()
            .ToList();

        var clientes = await _context.Clientes
            .Include(c => c.Direcciones)
            .Where(c => idsClientes.Contains(c.Id) && c.Estado && c.EstadoRegistro)
            .ToDictionaryAsync(c => c.Id);

        var productosLocal = await _context.ProductosLocal
            .Include(pl => pl.Producto)
            .Include(pl => pl.Local)
            .Where(pl => idsLocales.Contains(pl.IdLocal)
                && idsProductos.Contains(pl.IdProducto)
                && pl.Estado && pl.EstadoRegistro
                && pl.Producto.Estado && pl.Producto.EstadoRegistro)
            .ToListAsync();

        var disponibles = productosLocal
            .ToDictionary(pl => (pl.IdLocal, pl.IdProducto));

        var pedido = new Pedido
        {
            IdRepartidor = repartidor?.Id,
            Estado = EstadosPedido.Pendiente
        };

        var paresUsados = new HashSet<(long, long)>();

        for (var i = 0; i < modelo.Clientes.Count; i++)
        {
            var linea = modelo.Clientes[i];
            var prefijo = $"clientes[{i}]";

            if (!clientes.TryGetValue(linea.IdCliente, out var cliente))
            {
                errores.Add(new ErrorPedido($"{prefijo}.idCliente", "El cliente no existe o está inactivo."));
                continue;
            }

            var direccion = cliente.Direcciones
                .FirstOrDefault(d => d.Id == linea.IdDireccion && d.EstadoRegistro);

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

                if (!disponibles.TryGetValue((detalle.IdLocal, detalle.IdProducto), out var productoLocal))
                {
                    errores.Add(new ErrorPedido(
                        $"{campo}.idProducto",
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

                var precio = productoLocal.Producto.PrecioVenta;
                var bruto = detalle.Cantidad * precio;

                if (detalle.DescuentoMonto > bruto)
                {
                    errores.Add(new ErrorPedido(
                        $"{campo}.descuentoMonto",
                        $"El descuento no puede superar el monto del producto ({bruto:0.00})."));
                    continue;
                }

                var subtotal = bruto - detalle.DescuentoMonto;

                pedidoCliente.Detalles.Add(new DetallePedido
                {
                    IdLocal = detalle.IdLocal,
                    Local = productoLocal.Local,
                    IdProducto = detalle.IdProducto,
                    Producto = productoLocal.Producto,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = precio,
                    DescuentoMonto = detalle.DescuentoMonto,
                    Subtotal = subtotal
                });
            }

            pedidoCliente.Subtotal = pedidoCliente.Detalles.Sum(d => d.Subtotal);
            pedido.Clientes.Add(pedidoCliente);
        }

        // ❌ Validación de stock eliminada (el módulo de inventario aún no está implementado).

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
                pedidoExistente.IdRepartidor = pedido.IdRepartidor;
                pedidoExistente.Total = pedido.Total;
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
            if (persistir && pedidoExistente == null)
            {
                pedido.Estado = EstadosPedido.Enviado;
                await _context.SaveChangesAsync();
            }
            else if (persistir && pedidoExistente != null)
            {
                pedidoExistente.Estado = EstadosPedido.Enviado;
                await _context.SaveChangesAsync();
                pedido = pedidoExistente;
            }

            await _context.Entry(pedido).Collection(p => p.Clientes).LoadAsync();
            foreach (var pc in pedido.Clientes)
            {
                await _context.Entry(pc).Reference(c => c.Cliente).LoadAsync();
                await _context.Entry(pc).Reference(c => c.Direccion).LoadAsync();
                await _context.Entry(pc).Collection(c => c.Detalles).LoadAsync();
                foreach (var d in pc.Detalles)
                {
                    await _context.Entry(d).Reference(x => x.Producto).LoadAsync();
                    await _context.Entry(d).Reference(x => x.Local).LoadAsync();
                }
            }

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

    private async Task<(List<ErrorPedido>, List<Pedido>?, Repartidor?)> ValidarEnvioVariosAsync(
        EnviarPedidosViewModel modelo)
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
            .Include(p => p.Clientes).ThenInclude(c => c.Cliente)
            .Include(p => p.Clientes).ThenInclude(c => c.Direccion)
            .Include(p => p.Clientes).ThenInclude(c => c.Detalles).ThenInclude(d => d.Producto)
            .Include(p => p.Clientes).ThenInclude(c => c.Detalles).ThenInclude(d => d.Local)
            .Where(p => ids.Contains(p.Id) && p.EstadoRegistro)
            .ToListAsync();

        if (pedidos.Count != ids.Count)
            errores.Add(new ErrorPedido("pedidos", "Algún pedido seleccionado no existe."));

        var noPendientes = pedidos.Where(p => p.Estado != EstadosPedido.Pendiente).ToList();
        if (noPendientes.Any())
            errores.Add(new ErrorPedido("pedidos", "Solo se pueden enviar pedidos en estado Pendiente."));

        return (errores, pedidos, repartidor);
    }

    public async Task<PedidoResultado> PreviewReintentarEntregaAsync(
        long idPedido,
        ReintentarEntregaViewModel modelo)
    {
        var (errores, pedido, repartidor) =
            await ValidarReintentoEntregaAsync(idPedido, modelo.IdRepartidor);

        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        var mensaje = PedidoMensajeBuilder.ConstruirVarios(
            new List<Pedido> { pedido! });

        return new PedidoResultado
        {
            IdPedido = pedido!.Id,
            Total = pedido.Total,
            Mensaje = mensaje,
            WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(
                repartidor!.Celular,
                mensaje)
        };
    }

    public async Task<PedidoResultado> ReintentarEntregaAsync(
        long idPedido,
        ReintentarEntregaViewModel modelo)
    {
        var (errores, pedido, repartidor) =
            await ValidarReintentoEntregaAsync(idPedido, modelo.IdRepartidor);

        if (errores.Count > 0)
            return new PedidoResultado { Errores = errores };

        pedido!.IdRepartidor = repartidor!.Id;
        pedido.Estado = EstadosPedido.Enviado;
        pedido.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var mensaje = PedidoMensajeBuilder.ConstruirVarios(
            new List<Pedido> { pedido });

        return new PedidoResultado
        {
            IdPedido = pedido.Id,
            Total = pedido.Total,
            Mensaje = "PEDIDO REENVIADO CORRECTAMENTE",
            WhatsappUrl = PedidoMensajeBuilder.ConstruirUrl(
                repartidor.Celular,
                mensaje)
        };
    }

    private async Task<(List<ErrorPedido> Errores, Pedido? Pedido, Repartidor? Repartidor)>
        ValidarReintentoEntregaAsync(
            long idPedido,
            long? idRepartidor)
    {
        var errores = new List<ErrorPedido>();

        Repartidor? repartidor = null;

        if (idRepartidor.HasValue)
        {
            repartidor = await _context.Repartidores
                .FirstOrDefaultAsync(r =>
                    r.Id == idRepartidor.Value &&
                    r.Estado &&
                    r.EstadoRegistro);
        }

        if (repartidor == null)
        {
            errores.Add(
                new ErrorPedido(
                    "idRepartidor",
                    "Selecciona un repartidor."));
        }
        else if (!CelularValido(repartidor.Celular))
        {
            errores.Add(
                new ErrorPedido(
                    "idRepartidor",
                    "El celular del repartidor no tiene un formato válido."));
        }

        var pedido = await _context.Pedidos
            .Include(p => p.Clientes)
                .ThenInclude(c => c.Cliente)
            .Include(p => p.Clientes)
                .ThenInclude(c => c.Direccion)
            .Include(p => p.Clientes)
                .ThenInclude(c => c.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(p => p.Clientes)
                .ThenInclude(c => c.Detalles)
                .ThenInclude(d => d.Local)
            .FirstOrDefaultAsync(p =>
                p.Id == idPedido &&
                p.EstadoRegistro);

        if (pedido == null)
        {
            errores.Add(
                new ErrorPedido(
                    "pedido",
                    "El pedido no existe."));
        }
        else if (pedido.Estado != EstadosPedido.ConIncidencia)
        {
            errores.Add(
                new ErrorPedido(
                    "pedido",
                    "Solo se puede reintentar un pedido con incidencia."));
        }

        return (errores, pedido, repartidor);
    }

    public async Task<PedidoResultado> CancelarConIncidenciaAsync(long idPedido)
    {
        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(p =>
                p.Id == idPedido &&
                p.EstadoRegistro);

        if (pedido == null)
        {
            return new PedidoResultado
            {
                Errores =
                {
                    new ErrorPedido(
                        "pedido",
                        "El pedido no existe.")
                }
            };
        }

        if (pedido.Estado != EstadosPedido.ConIncidencia)
        {
            return new PedidoResultado
            {
                Errores =
                {
                    new ErrorPedido(
                        "pedido",
                        "Solo se puede cancelar un pedido con incidencia.")
                }
            };
        }

        pedido.EstadoRegistro = false;
        pedido.FechaActualizacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new PedidoResultado
        {
            IdPedido = pedido.Id,
            Mensaje = "PEDIDO CANCELADO CORRECTAMENTE"
        };
    }

    public async Task<ClienteBusquedaViewModel?> BuscarClienteAsync(string tipo, string termino)
    {
        var valor = termino?.Trim();
        if (string.IsNullOrEmpty(valor)) return null;

        var query = _context.Clientes
            .Include(c => c.Direcciones.Where(d => d.EstadoRegistro))
            .Where(c => c.Estado && c.EstadoRegistro);

        var patron = valor.ToLowerInvariant();

        query = tipo?.ToLowerInvariant() switch
        {
            "telefono" => query.Where(c => c.Telefono != null && c.Telefono.ToLower().Contains(patron)),
            "dni" => query.Where(c => c.Dni != null && c.Dni.ToLower().Contains(patron)),
            "nombre" => query.Where(c => c.Nombre.ToLower().Contains(patron)),
            _ => query.Where(c =>
                (c.Telefono != null && c.Telefono.ToLower().Contains(patron)) ||
                (c.Dni != null && c.Dni.ToLower().Contains(patron)) ||
                c.Nombre.ToLower().Contains(patron))
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