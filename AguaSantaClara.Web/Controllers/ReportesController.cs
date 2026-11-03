using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Administradora,Gerente")]
public class ReportesController : Controller
{
    private readonly AppDbContext _context;

    public ReportesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index(string? filtro = null, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null)
    {
        return RedirectToAction(nameof(Ventas), new { filtro, fechaDesde, fechaHasta });
    }

    [HttpGet]
    public async Task<IActionResult> Ventas(string? filtro = null, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, string? format = null)
    {
        var vm = await ObtenerReporteVentasAsync(filtro, fechaDesde, fechaHasta);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new
            {
                success = true,
                filtroRapido = vm.FiltroRapido,
                fechaDesde = vm.FechaDesde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                fechaHasta = vm.FechaHasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                totalVentas = vm.TotalVentas,
                totalPagos = vm.TotalPagos,
                totalDeudas = vm.TotalDeudas,
                cantidadPedidos = vm.CantidadPedidos,
                cantidadPagos = vm.CantidadPagos,
                cantidadDeudas = vm.CantidadDeudas,
                pedidos = vm.Pedidos.Select(p => new
                {
                    numero = p.Numero,
                    cliente = p.Cliente,
                    fecha = p.Fecha.ToLocalTime().ToString("dd/MM/yyyy"),
                    estado = p.Estado,
                    total = p.Total
                }),
                pagos = vm.Pagos.Select(p => new
                {
                    cliente = p.Cliente,
                    fecha = p.Fecha.ToLocalTime().ToString("dd/MM/yyyy"),
                    metodo = p.MetodoPago,
                    monto = p.Monto
                }),
                deudas = vm.Deudas.Select(d => new
                {
                    cliente = d.Cliente,
                    fecha = d.Fecha.ToLocalTime().ToString("dd/MM/yyyy"),
                    vencimiento = d.FechaVencimiento.ToString("dd/MM/yyyy"),
                    monto = d.Monto
                })
            });
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Exportar(string? filtro = null, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, string formato = "excel")
    {
        var vm = await ObtenerReporteVentasAsync(filtro, fechaDesde, fechaHasta);

        var sufijoArchivo = vm.FechaDesde == vm.FechaHasta
            ? vm.FechaDesde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : $"{vm.FechaDesde:yyyyMMdd}-{vm.FechaHasta:yyyyMMdd}";

        if (string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var contenidoCsv = GenerarCsv(vm);
            return File(
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(contenidoCsv)).ToArray(),
                "text/csv; charset=utf-8",
                $"reporte-ventas-{sufijoArchivo}.csv");
        }

        var contenidoExcel = GenerarExcel(vm);
        return File(
            contenidoExcel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reporte-ventas-{sufijoArchivo}.xlsx");
    }

    private async Task<ReporteVentasViewModel> ObtenerReporteVentasAsync(string? filtro, DateOnly? fechaDesde, DateOnly? fechaHasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var filtroRapido = (filtro ?? "hoy").Trim().ToLowerInvariant();

        DateOnly desde;
        DateOnly hasta;

        if (filtroRapido == "ayer")
        {
            desde = hoy.AddDays(-1);
            hasta = hoy.AddDays(-1);
        }
        else if (filtroRapido == "semana")
        {
            // Inicio de semana (lunes)
            var delta = ((int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            desde = hoy.AddDays(-delta);
            hasta = hoy;
        }
        else if (fechaDesde.HasValue || fechaHasta.HasValue || filtroRapido == "personalizado")
        {
            filtroRapido = "personalizado";
            desde = fechaDesde ?? hoy;
            hasta = fechaHasta ?? hoy;
            if (desde > hasta)
            {
                (desde, hasta) = (hasta, desde);
            }
        }
        else
        {
            filtroRapido = "hoy";
            desde = hoy;
            hasta = hoy;
        }

        var inicioUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var finUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

        // 1. Pedidos
        var pedidos = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro && p.FechaCreacion >= inicioUtc && p.FechaCreacion < finUtc)
            .Include(p => p.Clientes)
                .ThenInclude(pc => pc.Cliente)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new PedidoReporteItemViewModel
            {
                Id = p.Id,
                Cliente = p.Clientes
                    .Where(pc => pc.EstadoRegistro && pc.Cliente != null)
                    .Select(pc => pc.Cliente.Nombre)
                    .FirstOrDefault() ?? "Cliente General",
                Fecha = p.FechaCreacion,
                Estado = p.Estado,
                Total = p.Total
            })
            .ToListAsync();

        // 2. Pagos
        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro && p.FechaPago >= inicioUtc && p.FechaPago < finUtc)
            .Include(p => p.Cliente)
            .Include(p => p.MetodoPago)
            .OrderByDescending(p => p.FechaPago)
            .Select(p => new PagoReporteItemViewModel
            {
                Id = p.Id,
                Cliente = p.Cliente.Nombre,
                Fecha = p.FechaPago,
                MetodoPago = p.MetodoPago.Nombre,
                Monto = p.Monto
            })
            .ToListAsync();

        // 3. Deudas (Saldo pendiente real del período descontando pagos recibidos)
        var deudasEntidades = await _context.Deudas
            .AsNoTracking()
            .Where(d => d.EstadoRegistro && d.FechaCreacion >= inicioUtc && d.FechaCreacion < finUtc)
            .Include(d => d.Cliente)
            .Include(d => d.Pedido)
            .OrderByDescending(d => d.FechaCreacion)
            .ToListAsync();

        var idsPedidosConDeuda = deudasEntidades.Select(d => d.IdPedido).Distinct().ToList();

        var pagosPorPedido = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.EstadoRegistro && idsPedidosConDeuda.Contains(p.IdPedido))
            .GroupBy(p => p.IdPedido)
            .Select(g => new { IdPedido = g.Key, TotalPagado = g.Sum(p => p.Monto) })
            .ToDictionaryAsync(x => x.IdPedido, x => x.TotalPagado);

        var deudas = new List<DeudaReporteItemViewModel>();
        foreach (var d in deudasEntidades)
        {
            if (string.Equals(d.Estado, EstadosDeuda.Pagada, StringComparison.OrdinalIgnoreCase))
                continue;

            decimal saldoPendiente;
            if (d.Pedido != null)
            {
                var pagado = pagosPorPedido.GetValueOrDefault(d.IdPedido, 0m);
                saldoPendiente = Math.Max(0m, d.Pedido.Total - pagado);
            }
            else
            {
                saldoPendiente = Math.Max(0m, d.Monto - d.MontoPagadoAlRegistrar);
            }

            if (saldoPendiente > 0)
            {
                deudas.Add(new DeudaReporteItemViewModel
                {
                    Id = d.Id,
                    Cliente = d.Cliente?.Nombre ?? "Cliente sin registrar",
                    Fecha = d.FechaCreacion,
                    FechaVencimiento = d.FechaVencimiento,
                    Monto = saldoPendiente
                });
            }
        }

        return new ReporteVentasViewModel
        {
            FiltroRapido = filtroRapido,
            FechaDesde = desde,
            FechaHasta = hasta,
            TotalVentas = pedidos.Sum(p => p.Total),
            TotalPagos = pagos.Sum(p => p.Monto),
            TotalDeudas = deudas.Sum(d => d.Monto),
            CantidadPedidos = pedidos.Count,
            CantidadPagos = pagos.Count,
            CantidadDeudas = deudas.Count,
            Pedidos = pedidos,
            Pagos = pagos,
            Deudas = deudas
        };
    }

    private static string GenerarCsv(ReporteVentasViewModel vm)
    {
        var sb = new StringBuilder();
        var cultura = new CultureInfo("es-PE");

        sb.AppendLine("REPORTE DE VENTAS - AGUA SANTA CLARA");
        sb.AppendLine($"Período:;{vm.FechaDesde:dd/MM/yyyy} al {vm.FechaHasta:dd/MM/yyyy}");
        sb.AppendLine($"Generado:;{DateTime.Now:dd/MM/yyyy HH:mm}");
        sb.AppendLine();

        sb.AppendLine("RESUMEN DE TOTALES CONSOLIDADOS");
        sb.AppendLine($"Total Ventas:;S/ {vm.TotalVentas.ToString("N2", cultura)}");
        sb.AppendLine($"Pagos Recibidos:;S/ {vm.TotalPagos.ToString("N2", cultura)}");
        sb.AppendLine($"Deudas Registradas:;S/ {vm.TotalDeudas.ToString("N2", cultura)}");
        sb.AppendLine($"Total Pedidos:;{vm.CantidadPedidos}");
        sb.AppendLine();

        sb.AppendLine("PEDIDOS DEL PERÍODO");
        sb.AppendLine("N°;Cliente;Fecha;Estado;Total");
        foreach (var p in vm.Pedidos)
        {
            sb.AppendLine($"{p.Numero};\"{p.Cliente.Replace("\"", "\"\"")}\";{p.Fecha.ToLocalTime():dd/MM/yyyy};{p.Estado};S/ {p.Total.ToString("N2", cultura)}");
        }
        sb.AppendLine();

        sb.AppendLine("PAGOS RECIBIDOS");
        sb.AppendLine("Cliente;Fecha;Método;Monto");
        foreach (var p in vm.Pagos)
        {
            sb.AppendLine($"\"{p.Cliente.Replace("\"", "\"\"")}\";{p.Fecha.ToLocalTime():dd/MM/yyyy};{p.MetodoPago};S/ {p.Monto.ToString("N2", cultura)}");
        }
        sb.AppendLine();

        sb.AppendLine("DEUDAS REGISTRADAS");
        sb.AppendLine("Cliente;Fecha;Vencimiento;Monto");
        foreach (var d in vm.Deudas)
        {
            sb.AppendLine($"\"{d.Cliente.Replace("\"", "\"\"")}\";{d.Fecha.ToLocalTime():dd/MM/yyyy};{d.FechaVencimiento:dd/MM/yyyy};S/ {d.Monto.ToString("N2", cultura)}");
        }

        return sb.ToString();
    }

    private static readonly DateOnly FechaBaseExcel = new(1899, 12, 30);

    private static byte[] GenerarExcel(ReporteVentasViewModel vm)
    {
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        const string nombreHoja = "Reporte de Ventas";

        XElement Celda(string referencia, object? valor, int estilo)
        {
            var celda = new XElement(spreadsheet + "c",
                new XAttribute("r", referencia),
                new XAttribute("s", estilo));

            switch (valor)
            {
                case null:
                    break;
                case decimal monto:
                    celda.Add(new XElement(spreadsheet + "v", monto.ToString(CultureInfo.InvariantCulture)));
                    break;
                case int entero:
                    celda.Add(new XElement(spreadsheet + "v", entero.ToString(CultureInfo.InvariantCulture)));
                    break;
                case DateOnly fecha:
                    var serial = fecha.DayNumber - FechaBaseExcel.DayNumber;
                    celda.Add(new XElement(spreadsheet + "v", serial.ToString(CultureInfo.InvariantCulture)));
                    break;
                default:
                    celda.Add(new XAttribute("t", "inlineStr"),
                        new XElement(spreadsheet + "is",
                            new XElement(spreadsheet + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                valor.ToString() ?? string.Empty)));
                    break;
            }

            return celda;
        }

        XElement Fila(int numero, IEnumerable<XElement> celdas, double? alto = null)
        {
            var fila = new XElement(spreadsheet + "row", new XAttribute("r", numero));
            if (alto.HasValue)
            {
                fila.Add(new XAttribute("ht", alto.Value.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("customHeight", 1));
            }
            fila.Add(celdas);
            return fila;
        }

        var sheetData = new XElement(spreadsheet + "sheetData");
        var filaActual = 1;

        // Título y Período
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", "REPORTE DE VENTAS - AGUA SANTA CLARA", 1) }, 24));
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", $"Período: {vm.FechaDesde:dd/MM/yyyy} al {vm.FechaHasta:dd/MM/yyyy}  ·  Generado el {DateTime.Now:dd/MM/yyyy HH:mm}", 2) }));
        filaActual++; // Espacio

        // Sección KPIs / Resumen
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", "TOTAL VENTAS", 3), Celda($"B{filaActual - 1}", "PAGOS RECIBIDOS", 3), Celda($"C{filaActual - 1}", "DEUDAS REGISTRADAS", 3), Celda($"D{filaActual - 1}", "PEDIDOS DEL PERÍODO", 3) }, 20));
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", vm.TotalVentas, 5), Celda($"B{filaActual - 1}", vm.TotalPagos, 5), Celda($"C{filaActual - 1}", vm.TotalDeudas, 5), Celda($"D{filaActual - 1}", vm.CantidadPedidos, 6) }, 22));
        filaActual += 2; // Espacio

        // Sección 1: Pedidos del período
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", $"PEDIDOS DEL PERÍODO ({vm.CantidadPedidos})", 1) }, 20));
        sheetData.Add(Fila(filaActual++, new[]
        {
            Celda($"A{filaActual - 1}", "N°", 3),
            Celda($"B{filaActual - 1}", "CLIENTE", 3),
            Celda($"C{filaActual - 1}", "FECHA", 3),
            Celda($"D{filaActual - 1}", "ESTADO", 3),
            Celda($"E{filaActual - 1}", "TOTAL", 3)
        }, 22));

        if (vm.Pedidos.Count == 0)
        {
            sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", "No hay pedidos en el período seleccionado.", 6) }));
        }
        else
        {
            foreach (var p in vm.Pedidos)
            {
                sheetData.Add(Fila(filaActual++, new[]
                {
                    Celda($"A{filaActual - 1}", p.Numero, 6),
                    Celda($"B{filaActual - 1}", p.Cliente, 6),
                    Celda($"C{filaActual - 1}", DateOnly.FromDateTime(p.Fecha.ToLocalTime()), 4),
                    Celda($"D{filaActual - 1}", p.Estado, 6),
                    Celda($"E{filaActual - 1}", p.Total, 5)
                }));
            }
            sheetData.Add(Fila(filaActual++, new[]
            {
                Celda($"A{filaActual - 1}", "TOTAL", 7),
                Celda($"B{filaActual - 1}", null, 9),
                Celda($"C{filaActual - 1}", null, 9),
                Celda($"D{filaActual - 1}", null, 9),
                Celda($"E{filaActual - 1}", vm.TotalVentas, 8)
            }));
        }
        filaActual += 2; // Espacio

        // Sección 2: Pagos recibidos
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", $"PAGOS RECIBIDOS ({vm.CantidadPagos})", 1) }, 20));
        sheetData.Add(Fila(filaActual++, new[]
        {
            Celda($"A{filaActual - 1}", "CLIENTE", 3),
            Celda($"B{filaActual - 1}", "FECHA", 3),
            Celda($"C{filaActual - 1}", "MÉTODO", 3),
            Celda($"D{filaActual - 1}", "MONTO", 3)
        }, 22));

        if (vm.Pagos.Count == 0)
        {
            sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", "No hay pagos recibidos en el período seleccionado.", 6) }));
        }
        else
        {
            foreach (var p in vm.Pagos)
            {
                sheetData.Add(Fila(filaActual++, new[]
                {
                    Celda($"A{filaActual - 1}", p.Cliente, 6),
                    Celda($"B{filaActual - 1}", DateOnly.FromDateTime(p.Fecha.ToLocalTime()), 4),
                    Celda($"C{filaActual - 1}", p.MetodoPago, 6),
                    Celda($"D{filaActual - 1}", p.Monto, 5)
                }));
            }
            sheetData.Add(Fila(filaActual++, new[]
            {
                Celda($"A{filaActual - 1}", "TOTAL", 7),
                Celda($"B{filaActual - 1}", null, 9),
                Celda($"C{filaActual - 1}", null, 9),
                Celda($"D{filaActual - 1}", vm.TotalPagos, 8)
            }));
        }
        filaActual += 2; // Espacio

        // Sección 3: Deudas registradas
        sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", $"DEUDAS REGISTRADAS ({vm.CantidadDeudas})", 1) }, 20));
        sheetData.Add(Fila(filaActual++, new[]
        {
            Celda($"A{filaActual - 1}", "CLIENTE", 3),
            Celda($"B{filaActual - 1}", "FECHA", 3),
            Celda($"C{filaActual - 1}", "VENCIMIENTO", 3),
            Celda($"D{filaActual - 1}", "MONTO", 3)
        }, 22));

        if (vm.Deudas.Count == 0)
        {
            sheetData.Add(Fila(filaActual++, new[] { Celda($"A{filaActual - 1}", "No hay deudas registradas en el período seleccionado.", 6) }));
        }
        else
        {
            foreach (var d in vm.Deudas)
            {
                sheetData.Add(Fila(filaActual++, new[]
                {
                    Celda($"A{filaActual - 1}", d.Cliente, 6),
                    Celda($"B{filaActual - 1}", DateOnly.FromDateTime(d.Fecha.ToLocalTime()), 4),
                    Celda($"C{filaActual - 1}", DateOnly.FromDateTime(d.FechaVencimiento), 4),
                    Celda($"D{filaActual - 1}", d.Monto, 5)
                }));
            }
            sheetData.Add(Fila(filaActual++, new[]
            {
                Celda($"A{filaActual - 1}", "TOTAL", 7),
                Celda($"B{filaActual - 1}", null, 9),
                Celda($"C{filaActual - 1}", null, 9),
                Celda($"D{filaActual - 1}", vm.TotalDeudas, 8)
            }));
        }

        var columnasConfig = new (double Ancho, int Indice)[]
        {
            (18, 1), // A
            (30, 2), // B
            (16, 3), // C
            (18, 4), // D
            (18, 5)  // E
        };

        var worksheet = new XElement(spreadsheet + "worksheet",
            new XElement(spreadsheet + "sheetViews",
                new XElement(spreadsheet + "sheetView",
                    new XAttribute("showGridLines", 1),
                    new XAttribute("workbookViewId", 0))),
            new XElement(spreadsheet + "cols",
                columnasConfig.Select(c => new XElement(spreadsheet + "col",
                    new XAttribute("min", c.Indice),
                    new XAttribute("max", c.Indice),
                    new XAttribute("width", c.Ancho.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("customWidth", 1)))),
            sheetData);

        XElement Borde(string? color)
        {
            XElement Lado(string nombre) => color == null
                ? new XElement(spreadsheet + nombre)
                : new XElement(spreadsheet + nombre,
                    new XAttribute("style", "thin"),
                    new XElement(spreadsheet + "color", new XAttribute("rgb", color)));

            return new XElement(spreadsheet + "border",
                Lado("left"), Lado("right"), Lado("top"), Lado("bottom"),
                new XElement(spreadsheet + "diagonal"));
        }

        XElement Fuente(double tamano, bool negrita = false, string color = "FF1D1D1F") =>
            new(spreadsheet + "font",
                negrita ? new XElement(spreadsheet + "b") : null,
                new XElement(spreadsheet + "sz", new XAttribute("val", tamano.ToString(CultureInfo.InvariantCulture))),
                new XElement(spreadsheet + "color", new XAttribute("rgb", color)),
                new XElement(spreadsheet + "name", new XAttribute("val", "Calibri")));

        XElement Relleno(string tipo, string? color = null) =>
            new(spreadsheet + "fill",
                new XElement(spreadsheet + "patternFill",
                    new XAttribute("patternType", tipo),
                    color == null ? null : new XElement(spreadsheet + "fgColor", new XAttribute("rgb", color))));

        XElement Xf(int formato, int fuente, int relleno, int borde, string? horizontal = null, bool ajustarTexto = false)
        {
            var xf = new XElement(spreadsheet + "xf",
                new XAttribute("numFmtId", formato),
                new XAttribute("fontId", fuente),
                new XAttribute("fillId", relleno),
                new XAttribute("borderId", borde),
                new XAttribute("xfId", 0));
            if (formato != 0) xf.Add(new XAttribute("applyNumberFormat", 1));
            if (fuente != 0) xf.Add(new XAttribute("applyFont", 1));
            if (relleno != 0) xf.Add(new XAttribute("applyFill", 1));
            if (borde != 0) xf.Add(new XAttribute("applyBorder", 1));
            if (horizontal != null || ajustarTexto)
            {
                xf.Add(new XAttribute("applyAlignment", 1),
                    new XElement(spreadsheet + "alignment",
                        horizontal == null ? null : new XAttribute("horizontal", horizontal),
                        new XAttribute("vertical", "center"),
                        ajustarTexto ? new XAttribute("wrapText", 1) : null));
            }
            return xf;
        }

        const int formatoFecha = 164;
        const int formatoSoles = 165;
        var estilos = new XElement(spreadsheet + "styleSheet",
            new XElement(spreadsheet + "numFmts", new XAttribute("count", 2),
                new XElement(spreadsheet + "numFmt",
                    new XAttribute("numFmtId", formatoFecha),
                    new XAttribute("formatCode", "dd/mm/yyyy")),
                new XElement(spreadsheet + "numFmt",
                    new XAttribute("numFmtId", formatoSoles),
                    new XAttribute("formatCode", "\"S/ \"#,##0.00;[Red]\"-S/ \"#,##0.00"))),
            new XElement(spreadsheet + "fonts", new XAttribute("count", 5),
                Fuente(11),
                Fuente(14, negrita: true),
                Fuente(10, color: "FF6E6E73"),
                Fuente(11, negrita: true, color: "FFFFFFFF"),
                Fuente(11, negrita: true)),
            new XElement(spreadsheet + "fills", new XAttribute("count", 4),
                Relleno("none"),
                Relleno("gray125"),
                Relleno("solid", "FF1D1D1F"),
                Relleno("solid", "FFF2F4F7")),
            new XElement(spreadsheet + "borders", new XAttribute("count", 2),
                Borde(null),
                Borde("FFD9D9DF")),
            new XElement(spreadsheet + "cellStyleXfs", new XAttribute("count", 1),
                new XElement(spreadsheet + "xf",
                    new XAttribute("numFmtId", 0),
                    new XAttribute("fontId", 0),
                    new XAttribute("fillId", 0),
                    new XAttribute("borderId", 0))),
            new XElement(spreadsheet + "cellXfs", new XAttribute("count", 10),
                Xf(0, 0, 0, 0),
                Xf(0, 1, 0, 0),                                  // 1: EstiloTitulo
                Xf(0, 2, 0, 0),                                  // 2: EstiloSubtitulo
                Xf(0, 3, 2, 1, "center", ajustarTexto: true),    // 3: EstiloEncabezado
                Xf(formatoFecha, 0, 0, 1, "center"),             // 4: EstiloFecha
                Xf(formatoSoles, 0, 0, 1, "right"),              // 5: EstiloMonto
                Xf(0, 0, 0, 1, "left", ajustarTexto: true),      // 6: EstiloTexto
                Xf(0, 4, 3, 1, "center"),                        // 7: EstiloTotalEtiqueta
                Xf(formatoSoles, 4, 3, 1, "right"),              // 8: EstiloTotalMonto
                Xf(0, 4, 3, 1)),                                 // 9: EstiloTotalVacio
            new XElement(spreadsheet + "cellStyles", new XAttribute("count", 1),
                new XElement(spreadsheet + "cellStyle",
                    new XAttribute("name", "Normal"),
                    new XAttribute("xfId", 0),
                    new XAttribute("builtinId", 0))));

        var entradas = new Dictionary<string, XDocument>
        {
            ["[Content_Types].xml"] = new XDocument(
                new XElement(contentTypes + "Types",
                    new XElement(contentTypes + "Override",
                        new XAttribute("PartName", "/xl/styles.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
                    new XElement(contentTypes + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(contentTypes + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(contentTypes + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(contentTypes + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))),
            ["_rels/.rels"] = new XDocument(
                new XElement(packageRelationships + "Relationships",
                    new XElement(packageRelationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")))),
            ["xl/workbook.xml"] = new XDocument(
                new XElement(spreadsheet + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", relationships),
                    new XElement(spreadsheet + "sheets",
                        new XElement(spreadsheet + "sheet",
                            new XAttribute("name", nombreHoja),
                            new XAttribute("sheetId", "1"),
                            new XAttribute(relationships + "id", "rId1"))))),
            ["xl/_rels/workbook.xml.rels"] = new XDocument(
                new XElement(packageRelationships + "Relationships",
                    new XElement(packageRelationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")),
                    new XElement(packageRelationships + "Relationship",
                        new XAttribute("Id", "rId2"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                        new XAttribute("Target", "styles.xml")))),
            ["xl/styles.xml"] = new XDocument(estilos),
            ["xl/worksheets/sheet1.xml"] = new XDocument(worksheet)
        };

        using var memoria = new MemoryStream();
        using (var archivo = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ruta, documento) in entradas)
            {
                var entrada = archivo.CreateEntry(ruta, CompressionLevel.Optimal);
                using var escritor = new StreamWriter(entrada.Open(), new UTF8Encoding(false));
                documento.Save(escritor);
            }
        }

        return memoria.ToArray();
    }
}
