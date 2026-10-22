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

    public async Task<IActionResult> Index()
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierres = await _context.CierresCaja
            .AsNoTracking()
            .Where(cierre => cierre.IdVendedora == idVendedora && cierre.EstadoRegistro)
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
        await _context.SaveChangesAsync();

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
    public async Task<IActionResult> Exportar()
    {
        if (!ObtenerIdVendedora(out var idVendedora))
            return Unauthorized();

        var cierres = await _context.CierresCaja
            .AsNoTracking()
            .Where(cierre => cierre.IdVendedora == idVendedora && cierre.EstadoRegistro)
            .OrderBy(cierre => cierre.Fecha)
            .ToListAsync();

        var contenido = GenerarExcel(cierres);
        var nombreArchivo = $"cierres-caja-{DateTime.Today:yyyyMMdd}.xlsx";

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

    private static byte[] GenerarExcel(IReadOnlyList<CierreCaja> cierres)
    {
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        var columnas = new[]
        {
            "Fecha",
            "Efectivo esperado (S/)",
            "Yape esperado (S/)",
            "Total esperado (S/)",
            "Efectivo recibido (S/)",
            "Yape recibido (S/)",
            "Total recibido (S/)",
            "Gastos del día (S/)",
            "Diferencia (S/)",
            "Estado",
            "Observaciones"
        };
        var filas = new List<IReadOnlyList<object?>>
        {
            columnas.Cast<object?>().ToArray()
        };

        filas.AddRange(cierres.Select(cierre => (IReadOnlyList<object?>)new object?[]
        {
            cierre.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            cierre.EfectivoEsperado,
            cierre.YapeEsperado,
            cierre.EfectivoEsperado + cierre.YapeEsperado,
            cierre.EfectivoRecibido,
            cierre.YapeRecibido,
            cierre.EfectivoRecibido + cierre.YapeRecibido,
            cierre.GastosDelDia,
            cierre.Diferencia,
            cierre.Estado,
            cierre.Observacion ?? string.Empty
        }));

        var sheetData = new XElement(spreadsheet + "sheetData");
        for (var indiceFila = 0; indiceFila < filas.Count; indiceFila++)
        {
            var fila = new XElement(spreadsheet + "row", new XAttribute("r", indiceFila + 1));
            for (var indiceColumna = 0; indiceColumna < columnas.Length; indiceColumna++)
            {
                var valor = filas[indiceFila][indiceColumna];
                var referencia = $"{NumeroColumnaExcel(indiceColumna + 1)}{indiceFila + 1}";
                if (valor is decimal monto)
                {
                    fila.Add(new XElement(spreadsheet + "c",
                        new XAttribute("r", referencia),
                        new XElement(spreadsheet + "v", monto.ToString(CultureInfo.InvariantCulture))));
                }
                else
                {
                    fila.Add(new XElement(spreadsheet + "c",
                        new XAttribute("r", referencia),
                        new XAttribute("t", "inlineStr"),
                        new XElement(spreadsheet + "is",
                            new XElement(spreadsheet + "t",
                                new XAttribute(XNamespace.Xml + "space", "preserve"),
                                valor?.ToString() ?? string.Empty))));
                }
            }

            sheetData.Add(fila);
        }

        var entradas = new Dictionary<string, XDocument>
        {
            ["[Content_Types].xml"] = new XDocument(
                new XElement(contentTypes + "Types",
                    new XAttribute(XNamespace.Xmlns + "r", relationships),
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
                            new XAttribute("name", "Cierres de caja"),
                            new XAttribute("sheetId", "1"),
                            new XAttribute(relationships + "id", "rId1"))))),
            ["xl/_rels/workbook.xml.rels"] = new XDocument(
                new XElement(packageRelationships + "Relationships",
                    new XElement(packageRelationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")))),
            ["xl/worksheets/sheet1.xml"] = new XDocument(
                new XElement(spreadsheet + "worksheet", sheetData))
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
