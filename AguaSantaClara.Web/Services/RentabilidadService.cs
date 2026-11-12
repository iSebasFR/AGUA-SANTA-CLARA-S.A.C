using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Services;

// HU-21: el costo usa Producto.Costo actual porque no existe costo histórico por venta.
// Cuentan los mismos pedidos que el Reporte de Ventas (EstadoRegistro activo, sin filtrar por Estado).
public class RentabilidadService : IRentabilidadService
{
    public const string LineaBidonesGalones = "Bidones/Galones";
    public const string LineaPapel = "Papel higiénico";
    public const string LineaHielo = "Hielo";
    public const string LineaOtros = "Otros";
    public const string SinCategoria = "(sin categoría)";

    private static readonly string[] LineasFijas = { LineaBidonesGalones, LineaPapel, LineaHielo };

    private readonly AppDbContext _context;

    public RentabilidadService(AppDbContext context) => _context = context;

    public async Task<RentabilidadViewModel> ObtenerRentabilidadAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var inicioUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var finUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

        var ventas = await _context.DetallesPedido
            .AsNoTracking()
            .Where(d => d.EstadoRegistro
                && d.PedidoCliente.EstadoRegistro
                && d.PedidoCliente.Pedido.EstadoRegistro
                && d.PedidoCliente.Pedido.FechaCreacion >= inicioUtc
                && d.PedidoCliente.Pedido.FechaCreacion < finUtc)
            .GroupBy(d => new
            {
                d.IdProducto,
                d.Producto.Nombre,
                d.Producto.Categoria,
                d.Producto.PrecioVenta,
                d.Producto.Costo
            })
            .Select(g => new
            {
                g.Key.IdProducto,
                g.Key.Nombre,
                g.Key.Categoria,
                g.Key.PrecioVenta,
                g.Key.Costo,
                Cantidad = g.Sum(d => d.Cantidad),
                IngresoBruto = g.Sum(d => d.Cantidad * d.PrecioUnitario),
                Descuentos = g.Sum(d => d.DescuentoMonto)
            })
            .ToListAsync(ct);

        var productos = ventas.Select(v =>
        {
            var ingresoNeto = v.IngresoBruto - v.Descuentos;
            var costo = v.Cantidad * v.Costo;
            var margen = ingresoNeto - costo;
            return new ProductoRentabilidadViewModel
            {
                IdProducto = v.IdProducto,
                Nombre = v.Nombre,
                Categoria = v.Categoria,
                Cantidad = v.Cantidad,
                PrecioVenta = v.PrecioVenta,
                CostoUnitario = v.Costo,
                IngresoBruto = v.IngresoBruto,
                Descuentos = v.Descuentos,
                IngresoNeto = ingresoNeto,
                Costo = costo,
                Margen = margen,
                PorcentajeMargen = Porcentaje(margen, ingresoNeto)
            };
        }).ToList();

        var porLinea = productos.ToLookup(p => ObtenerLinea(p.Categoria));
        var nombresLineas = porLinea.Contains(LineaOtros) ? LineasFijas.Append(LineaOtros) : LineasFijas;
        var lineas = nombresLineas.Select(nombre => CrearLinea(nombre, porLinea[nombre])).ToList();

        var ingresoNetoTotal = lineas.Sum(l => l.IngresoNeto);
        var margenTotal = lineas.Sum(l => l.Margen);

        return new RentabilidadViewModel
        {
            FechaDesde = desde,
            FechaHasta = hasta,
            IngresoNeto = ingresoNetoTotal,
            Costo = lineas.Sum(l => l.Costo),
            Margen = margenTotal,
            PorcentajeMargen = Porcentaje(margenTotal, ingresoNetoTotal),
            Lineas = lineas,
            CategoriasSinLinea = porLinea[LineaOtros]
                .Select(p => string.IsNullOrWhiteSpace(p.Categoria) ? SinCategoria : p.Categoria.Trim())
                .Distinct()
                .Order(StringComparer.CurrentCulture)
                .ToList()
        };
    }

    public static string ObtenerLinea(string? categoria) => categoria?.Trim().ToLowerInvariant() switch
    {
        "bidón" or "galón" => LineaBidonesGalones,
        "papel" or "papel higiénico" => LineaPapel,
        "hielo" => LineaHielo,
        _ => LineaOtros
    };

    private static LineaRentabilidadViewModel CrearLinea(string nombre, IEnumerable<ProductoRentabilidadViewModel> productos)
    {
        var lista = productos.OrderByDescending(p => p.Margen).ThenBy(p => p.Nombre).ToList();
        var ingresoNeto = lista.Sum(p => p.IngresoNeto);
        var margen = lista.Sum(p => p.Margen);

        return new LineaRentabilidadViewModel
        {
            Nombre = nombre,
            IngresoBruto = lista.Sum(p => p.IngresoBruto),
            Descuentos = lista.Sum(p => p.Descuentos),
            IngresoNeto = ingresoNeto,
            Costo = lista.Sum(p => p.Costo),
            Margen = margen,
            PorcentajeMargen = Porcentaje(margen, ingresoNeto),
            Productos = lista
        };
    }

    private static decimal? Porcentaje(decimal margen, decimal ingresoNeto) =>
        ingresoNeto == 0m ? null : margen / ingresoNeto * 100m;
}
