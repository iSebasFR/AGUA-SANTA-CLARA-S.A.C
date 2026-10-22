using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora")]
public class CierreCajaController : Controller
{
    private readonly AppDbContext _context;

    public CierreCajaController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(DateOnly? fechaDesde = null, DateOnly? fechaHasta = null)
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierresQuery = _context.CierresCaja
            .AsNoTracking()
            .Where(cierre => cierre.IdVendedora == idVendedora && cierre.EstadoRegistro);

        var errorFiltro = !ModelState.IsValid
            ? "Ingresa fechas válidas para aplicar el filtro."
            : ValidarRangoFechas(fechaDesde, fechaHasta);
        if (errorFiltro != null)
        {
            ViewBag.ErrorFiltroFechas = errorFiltro;
            cierresQuery = cierresQuery.Where(_ => false);
        }
        else
        {
            cierresQuery = AplicarRangoFechas(cierresQuery, fechaDesde, fechaHasta);
        }

        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ViewBag.TieneFiltrosFecha = fechaDesde.HasValue || fechaHasta.HasValue;

        var cierres = await cierresQuery
            .OrderByDescending(cierre => cierre.Fecha)
            .ToListAsync();

        return View(cierres);
    }

    [HttpGet]
    public async Task<IActionResult> Resumen(DateOnly fecha)
    {
        if (!ModelState.IsValid || fecha == default)
            return BadRequest(new { message = "Selecciona una fecha válida." });

        var montosEsperados = await CalcularMontosEsperadosAsync(fecha);
        return Json(new
        {
            efectivoEsperado = montosEsperados.Efectivo,
            yapeEsperado = montosEsperados.Yape,
            montoEsperado = montosEsperados.Efectivo + montosEsperados.Yape
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CierreCajaInputViewModel modelo)
    {
        if (modelo.Fecha == default)
            ModelState.AddModelError(nameof(modelo.Fecha), "La fecha es obligatoria.");

        if (decimal.Round(modelo.EfectivoRecibido, 2) != modelo.EfectivoRecibido
            || decimal.Round(modelo.YapeRecibido, 2) != modelo.YapeRecibido
            || decimal.Round(modelo.GastosDelDia, 2) != modelo.GastosDelDia)
        {
            ModelState.AddModelError(string.Empty, "Los montos pueden tener como máximo dos decimales.");
        }

        if (!ModelState.IsValid)
        {
            var errores = ModelState
                .Where(campo => campo.Value?.Errors.Count > 0)
                .SelectMany(campo => campo.Value!.Errors.Select(error => error.ErrorMessage))
                .Where(mensaje => !string.IsNullOrWhiteSpace(mensaje))
                .Distinct()
                .ToArray();

            return BadRequest(new
            {
                success = false,
                message = errores.Length > 0
                    ? string.Join(" ", errores)
                    : "Revisa los datos ingresados."
            });
        }

        var idVendedoraTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(idVendedoraTexto, NumberStyles.None, CultureInfo.InvariantCulture, out var idVendedora))
            return Unauthorized(new { success = false, message = "No se pudo identificar a la vendedora." });

        var vendedoraExiste = await _context.Usuarios
            .AnyAsync(usuario => usuario.Id == idVendedora && usuario.EstadoRegistro);
        if (!vendedoraExiste)
            return Unauthorized(new { success = false, message = "La cuenta de la vendedora ya no está activa." });

        var cierreExistente = await _context.CierresCaja
            .AnyAsync(cierre => cierre.IdVendedora == idVendedora
                && cierre.Fecha == modelo.Fecha
                && cierre.EstadoRegistro);
        if (cierreExistente)
            return Conflict(new { success = false, message = "Ya existe un cierre registrado para esta fecha." });

        var montosEsperados = await CalcularMontosEsperadosAsync(modelo.Fecha);
        var diferencia = modelo.EfectivoRecibido + modelo.YapeRecibido
            - montosEsperados.Efectivo - montosEsperados.Yape;

        var cierre = new CierreCaja
        {
            IdVendedora = idVendedora,
            Fecha = modelo.Fecha,
            EfectivoRecibido = modelo.EfectivoRecibido,
            YapeRecibido = modelo.YapeRecibido,
            GastosDelDia = modelo.GastosDelDia,
            EfectivoEsperado = montosEsperados.Efectivo,
            YapeEsperado = montosEsperados.Yape,
            Diferencia = diferencia,
            Estado = diferencia == 0m
                ? EstadosCierreCaja.Cuadrado
                : EstadosCierreCaja.ConDiferencia,
            Observacion = string.IsNullOrWhiteSpace(modelo.Observacion)
                ? null
                : modelo.Observacion.Trim(),
            EstadoRegistro = true
        };

        _context.CierresCaja.Add(cierre);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos envíos simultáneos: el índice único (vendedora, fecha) rechaza el segundo
            return Conflict(new { success = false, message = "Ya existe un cierre registrado para esta fecha." });
        }

        return Json(new
        {
            success = true,
            message = "cierre de caja registrado correctamente",
            id = cierre.Id,
            fecha = cierre.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            efectivoEsperado = cierre.EfectivoEsperado,
            yapeEsperado = cierre.YapeEsperado,
            efectivoRecibido = cierre.EfectivoRecibido,
            yapeRecibido = cierre.YapeRecibido,
            gastosDelDia = cierre.GastosDelDia,
            totalEnCaja = cierre.TotalEnCaja,
            diferencia = cierre.Diferencia,
            estado = cierre.Estado
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierre = await _context.CierresCaja
            .AsNoTracking()
            .Include(registro => registro.Vendedora)
            .FirstOrDefaultAsync(registro => registro.Id == id
                && registro.IdVendedora == idVendedora
                && registro.EstadoRegistro);

        return cierre == null ? NotFound() : View(cierre);
    }

    [HttpGet]
    public async Task<IActionResult> Exportar(DateOnly? fechaDesde = null, DateOnly? fechaHasta = null)
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var errorFiltro = !ModelState.IsValid
            ? "Ingresa fechas válidas para aplicar el filtro."
            : ValidarRangoFechas(fechaDesde, fechaHasta);
        if (errorFiltro != null)
            return BadRequest(errorFiltro);

        var cierresQuery = _context.CierresCaja
            .AsNoTracking()
            .Where(cierre => cierre.IdVendedora == idVendedora && cierre.EstadoRegistro);
        cierresQuery = AplicarRangoFechas(cierresQuery, fechaDesde, fechaHasta);

        // Orden cronológico (más antiguo primero) para que el reporte se lea de arriba hacia abajo
        var cierres = await cierresQuery
            .OrderBy(cierre => cierre.Fecha)
            .ThenBy(cierre => cierre.Id)
            .ToListAsync();

        var contenido = GenerarExcel(cierres, fechaDesde, fechaHasta);
        var nombreArchivo = fechaDesde.HasValue && fechaDesde == fechaHasta
            ? $"cierre-caja-{fechaDesde.Value:yyyyMMdd}.xlsx"
            : $"cierres-caja-{DateTime.Today:yyyyMMdd}.xlsx";

        return File(
            contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            nombreArchivo);
    }

    private async Task<(decimal Efectivo, decimal Yape)> CalcularMontosEsperadosAsync(DateOnly fecha)
    {
        var inicioUtc = InicioDelDiaUtc(fecha);
        var finUtc = InicioDelDiaUtc(fecha.AddDays(1));

        var montosPorMetodo = await _context.Pagos
            .AsNoTracking()
            .Where(pago => pago.EstadoRegistro
                && pago.Pedido.EstadoRegistro
                && pago.FechaPago >= inicioUtc
                && pago.FechaPago < finUtc
                && (pago.MetodoPago.Nombre == "Efectivo" || pago.MetodoPago.Nombre == "Yape"))
            .GroupBy(pago => pago.MetodoPago.Nombre)
            .Select(grupo => new { Metodo = grupo.Key, Monto = grupo.Sum(pago => pago.Monto) })
            .ToListAsync();

        var efectivo = montosPorMetodo
            .Where(monto => string.Equals(monto.Metodo, "Efectivo", StringComparison.OrdinalIgnoreCase))
            .Sum(monto => monto.Monto);
        var yape = montosPorMetodo
            .Where(monto => string.Equals(monto.Metodo, "Yape", StringComparison.OrdinalIgnoreCase))
            .Sum(monto => monto.Monto);

        return (efectivo, yape);
    }

    private static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

    private static string? ValidarRangoFechas(DateOnly? fechaDesde, DateOnly? fechaHasta)
    {
        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
            return "La fecha inicial no puede ser posterior a la fecha final.";

        return null;
    }

    private static IQueryable<CierreCaja> AplicarRangoFechas(
        IQueryable<CierreCaja> cierres,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta)
    {
        if (fechaDesde.HasValue)
            cierres = cierres.Where(cierre => cierre.Fecha >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            cierres = cierres.Where(cierre => cierre.Fecha <= fechaHasta.Value);

        return cierres;
    }

    // Índices de cellXfs definidos en xl/styles.xml
    private const int EstiloTitulo = 1;
    private const int EstiloSubtitulo = 2;
    private const int EstiloEncabezado = 3;
    private const int EstiloFecha = 4;
    private const int EstiloMonto = 5;
    private const int EstiloTexto = 6;
    private const int EstiloTotalEtiqueta = 7;
    private const int EstiloTotalMonto = 8;
    private const int EstiloTotalVacio = 9;

    private static readonly DateOnly FechaBaseExcel = new(1899, 12, 30);

    private static byte[] GenerarExcel(
        IReadOnlyList<CierreCaja> cierres,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null)
    {
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        const string nombreHoja = "Cierres de caja";
        const int filaEncabezado = 4;

        var columnas = new (string Titulo, double Ancho, bool EsMonto)[]
        {
            ("Fecha", 13, false),
            ("Efectivo esperado", 18, true),
            ("Yape esperado", 16, true),
            ("Total esperado", 16, true),
            ("Efectivo recibido", 18, true),
            ("Yape recibido", 16, true),
            ("Gastos del día", 16, true),
            ("Total en caja", 16, true),
            ("Diferencia", 14, true),
            ("Estado", 16, false),
            ("Observaciones", 45, false)
        };
        var ultimaColumna = NumeroColumnaExcel(columnas.Length);

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
                case DateOnly fecha:
                    // Excel guarda las fechas como número de días desde 30/12/1899
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

        var periodo = (fechaDesde, fechaHasta) switch
        {
            ({ } desde, { } hasta) => $"Periodo: {desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} al {hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
            ({ } desde, null) => $"Periodo: desde {desde.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
            (null, { } hasta) => $"Periodo: hasta {hasta.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
            _ => "Periodo: todos los registros"
        };
        var generado = $"Generado el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}";

        var sheetData = new XElement(spreadsheet + "sheetData",
            Fila(1, new[] { Celda("A1", "Cierres de caja", EstiloTitulo) }, 24),
            Fila(2, new[] { Celda("A2", $"{periodo}  ·  {generado}", EstiloSubtitulo) }),
            Fila(filaEncabezado,
                columnas.Select((columna, indice) =>
                    Celda($"{NumeroColumnaExcel(indice + 1)}{filaEncabezado}", columna.Titulo, EstiloEncabezado)),
                22));

        var numeroFila = filaEncabezado;
        foreach (var cierre in cierres)
        {
            numeroFila++;
            var valores = new (object? Valor, int Estilo)[]
            {
                (cierre.Fecha, EstiloFecha),
                (cierre.EfectivoEsperado, EstiloMonto),
                (cierre.YapeEsperado, EstiloMonto),
                (cierre.EfectivoEsperado + cierre.YapeEsperado, EstiloMonto),
                (cierre.EfectivoRecibido, EstiloMonto),
                (cierre.YapeRecibido, EstiloMonto),
                (cierre.GastosDelDia, EstiloMonto),
                (cierre.TotalEnCaja, EstiloMonto),
                (cierre.Diferencia, EstiloMonto),
                (cierre.Estado, EstiloTexto),
                (cierre.Observacion ?? string.Empty, EstiloTexto)
            };
            var fila = numeroFila;
            sheetData.Add(Fila(fila, valores.Select((celda, indice) =>
                Celda($"{NumeroColumnaExcel(indice + 1)}{fila}", celda.Valor, celda.Estilo))));
        }

        var ultimaFilaDatos = numeroFila;
        if (cierres.Count > 0)
        {
            numeroFila++;
            var filaTotales = new XElement(spreadsheet + "row", new XAttribute("r", numeroFila),
                Celda($"A{numeroFila}", "TOTAL", EstiloTotalEtiqueta));
            for (var indice = 1; indice < columnas.Length; indice++)
            {
                var letra = NumeroColumnaExcel(indice + 1);
                var referencia = $"{letra}{numeroFila}";
                filaTotales.Add(columnas[indice].EsMonto
                    ? new XElement(spreadsheet + "c",
                        new XAttribute("r", referencia),
                        new XAttribute("s", EstiloTotalMonto),
                        new XElement(spreadsheet + "f", $"SUM({letra}{filaEncabezado + 1}:{letra}{ultimaFilaDatos})"))
                    : Celda(referencia, null, EstiloTotalVacio));
            }

            sheetData.Add(filaTotales);
        }

        var ultimaFilaFiltro = Math.Max(ultimaFilaDatos, filaEncabezado);
        var rangoFiltro = $"A{filaEncabezado}:{ultimaColumna}{ultimaFilaFiltro}";

        var worksheet = new XElement(spreadsheet + "worksheet",
            new XElement(spreadsheet + "sheetViews",
                new XElement(spreadsheet + "sheetView",
                    new XAttribute("showGridLines", 0),
                    new XAttribute("workbookViewId", 0),
                    new XElement(spreadsheet + "pane",
                        new XAttribute("ySplit", filaEncabezado),
                        new XAttribute("topLeftCell", $"A{filaEncabezado + 1}"),
                        new XAttribute("activePane", "bottomLeft"),
                        new XAttribute("state", "frozen")))),
            new XElement(spreadsheet + "cols",
                columnas.Select((columna, indice) => new XElement(spreadsheet + "col",
                    new XAttribute("min", indice + 1),
                    new XAttribute("max", indice + 1),
                    new XAttribute("width", columna.Ancho.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("customWidth", 1)))),
            sheetData,
            new XElement(spreadsheet + "autoFilter", new XAttribute("ref", rangoFiltro)),
            new XElement(spreadsheet + "pageSetup",
                new XAttribute("orientation", "landscape")));

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
                Fuente(16, negrita: true),
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
                Xf(0, 1, 0, 0),                                  // EstiloTitulo
                Xf(0, 2, 0, 0),                                  // EstiloSubtitulo
                Xf(0, 3, 2, 1, "center", ajustarTexto: true),    // EstiloEncabezado
                Xf(formatoFecha, 0, 0, 1, "center"),             // EstiloFecha
                Xf(formatoSoles, 0, 0, 1, "right"),              // EstiloMonto
                Xf(0, 0, 0, 1, "left", ajustarTexto: true),      // EstiloTexto
                Xf(0, 4, 3, 1, "center"),                        // EstiloTotalEtiqueta
                Xf(formatoSoles, 4, 3, 1, "right"),              // EstiloTotalMonto
                Xf(0, 4, 3, 1)),                                 // EstiloTotalVacio
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
                            new XAttribute(relationships + "id", "rId1"))),
                    new XElement(spreadsheet + "definedNames",
                        new XElement(spreadsheet + "definedName",
                            new XAttribute("name", "_xlnm._FilterDatabase"),
                            new XAttribute("localSheetId", 0),
                            new XAttribute("hidden", 1),
                            $"'{nombreHoja}'!$A${filaEncabezado}:${ultimaColumna}${ultimaFilaFiltro}")))),
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

    private static string NumeroColumnaExcel(int columna)
    {
        var nombre = new StringBuilder();
        while (columna > 0)
        {
            columna--;
            nombre.Insert(0, (char)('A' + columna % 26));
            columna /= 26;
        }

        return nombre.ToString();
    }

    private bool ObtenerIdVendedora(out long idVendedora)
    {
        var idVendedoraTexto = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(idVendedoraTexto, NumberStyles.None, CultureInfo.InvariantCulture, out idVendedora);
    }
}
