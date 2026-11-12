using AguaSantaClara.Tests.Soporte;
using AguaSantaClara.Web.Controllers;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Tests;

public class ProductosControllerTests
{
    private static AppDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(AppDbContext Db, Local Central, Local Norte, Local Inactivo)> CrearEscenarioAsync()
    {
        var db = CrearContexto();
        var central = new Local { Nombre = "Central" };
        var norte = new Local { Nombre = "Norte" };
        var inactivo = new Local { Nombre = "Cerrado", Estado = false };
        db.Locales.AddRange(central, norte, inactivo);
        await db.SaveChangesAsync();
        return (db, central, norte, inactivo);
    }

    private static async Task<Producto> AgregarProductoAsync(AppDbContext db, string nombre, string categoria,
        bool estado = true, params Local[] locales)
    {
        var producto = new Producto { Nombre = nombre, Categoria = categoria, PrecioVenta = 10m, Costo = 5m, Estado = estado };
        db.Productos.Add(producto);
        foreach (var local in locales)
            db.ProductosLocal.Add(new ProductoLocal { Producto = producto, IdLocal = local.Id, Estado = true, EstadoRegistro = true });
        await db.SaveChangesAsync();
        return producto;
    }

    private static ProductosController Controlador(AppDbContext db, bool ajax = false) =>
        ControladorFactory.Preparar(new ProductosController(db), ajax, rol: "Administradora");

    private static CrearProductoViewModel ModeloCrear(params long[] locales) => new()
    {
        Nombre = " Bidón 20L ",
        Descripcion = " Agua de mesa ",
        Categoria = "Bidón",
        PrecioVenta = 12m,
        Costo = 6m,
        Estado = true,
        IdLocales = locales.ToList()
    };

    // ---------- Index ----------

    [Fact]
    public async Task Index_FiltraPorNombreCategoriaYEstadoYExcluyeEliminados()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        await AgregarProductoAsync(db, "Bidón 20L", "Bidón", true, central);
        await AgregarProductoAsync(db, "Bidón 7L", "Bidón", false, central);
        await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", true, central);
        var eliminado = await AgregarProductoAsync(db, "Bidón viejo", "Bidón", true);
        eliminado.EstadoRegistro = false;
        await db.SaveChangesAsync();
        var controller = Controlador(db);

        ProductosIndexViewModel Modelo(IActionResult r) => Assert.IsType<ProductosIndexViewModel>(Assert.IsType<ViewResult>(r).Model);

        var todos = Modelo(await controller.Index(null, null, null));
        Assert.Equal(new[] { "Bidón 20L", "Bidón 7L", "Hielo 3kg" }, todos.Productos.Select(p => p.Nombre));

        Assert.Equal(new[] { "Bidón 20L", "Bidón 7L" },
            Modelo(await controller.Index(" Bidón ", null, null)).Productos.Select(p => p.Nombre));
        Assert.Equal("Hielo 3kg", Assert.Single(Modelo(await controller.Index(null, "Hielo", null)).Productos).Nombre);

        var filtrado = Modelo(await controller.Index("Bidón", "Bidón", false));
        Assert.Equal("Bidón 7L", Assert.Single(filtrado.Productos).Nombre);
        Assert.Equal("Bidón", filtrado.CategoriaFiltro);
        Assert.False(filtrado.EstadoFiltro);
    }

    // ---------- Create ----------

    [Fact]
    public async Task CreateGet_SinAjax_RedirigeAIndex()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();
        Assert.IsType<RedirectToActionResult>(await Controlador(db).Create());
    }

    [Fact]
    public async Task CreateGet_Ajax_DevuelveParcialSoloConLocalesActivos()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();

        var parcial = Assert.IsType<PartialViewResult>(await Controlador(db, ajax: true).Create());

        Assert.Equal("_Create", parcial.ViewName);
        var modelo = Assert.IsType<CrearProductoViewModel>(parcial.Model);
        Assert.Equal(new[] { "Central", "Norte" }, modelo.Locales.Select(l => l.Text));
    }

    [Fact]
    public async Task Create_DatosValidos_CreaProductoEnLocalesSeleccionados()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        var controller = Controlador(db);

        var resultado = await controller.Create(ModeloCrear(central.Id, norte.Id, norte.Id));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("PRODUCTO CREADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        var producto = await db.Productos.SingleAsync();
        Assert.Equal("Bidón 20L", producto.Nombre);
        Assert.Equal("Agua de mesa", producto.Descripcion);
        Assert.Equal("Bidón", producto.Categoria);
        Assert.Equal(12m, producto.PrecioVenta);
        Assert.Equal(6m, producto.Costo);
        Assert.True(producto.Estado);
        Assert.Equal(new[] { central.Id, norte.Id },
            (await db.ProductosLocal.Select(pl => pl.IdLocal).ToListAsync()).OrderBy(x => x));
    }

    [Fact]
    public async Task Create_DescripcionVacia_GuardaNull()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        var modelo = ModeloCrear(central.Id);
        modelo.Descripcion = "   ";

        await Controlador(db).Create(modelo);

        Assert.Null((await db.Productos.SingleAsync()).Descripcion);
    }

    [Fact]
    public async Task Create_SinLocales_RechazaYNoGuarda()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();
        var controller = Controlador(db);
        var modelo = ModeloCrear();
        modelo.IdLocales = null!;

        await controller.Create(modelo);

        Assert.Contains("Seleccione al menos un local.", (string)controller.TempData["ErrorMessage"]!);
        Assert.Empty(await db.Productos.ToListAsync());
    }

    [Fact]
    public async Task Create_LocalInactivoOInexistente_RechazaConParcialEnAjax()
    {
        var (db, central, _, inactivo) = await CrearEscenarioAsync();
        var controller = Controlador(db, ajax: true);

        var parcial = Assert.IsType<PartialViewResult>(await controller.Create(ModeloCrear(central.Id, inactivo.Id)));

        Assert.Equal("_Create", parcial.ViewName);
        Assert.Contains(controller.ModelState[nameof(CrearProductoViewModel.IdLocales)]!.Errors,
            e => e.ErrorMessage == "Seleccione únicamente locales activos válidos.");
        Assert.Empty(await db.Productos.ToListAsync());
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, 5)]
    public void CrearProductoViewModel_PrecioOCostoNoPositivo_EsInvalido(decimal precio, decimal costo)
    {
        var controller = new ProductosController(null!);
        var modelo = ModeloCrear(1);
        modelo.PrecioVenta = precio;
        modelo.Costo = costo;

        ControladorFactory.ValidarModelo(controller, modelo);

        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public void CrearProductoViewModel_SinCategoria_EsInvalido()
    {
        var controller = new ProductosController(null!);
        var modelo = ModeloCrear(1);
        modelo.Categoria = "";

        ControladorFactory.ValidarModelo(controller, modelo);

        Assert.Contains(controller.ModelState[nameof(CrearProductoViewModel.Categoria)]!.Errors,
            e => e.ErrorMessage == "Seleccione una categoría.");
    }

    [Fact]
    public async Task Create_NombreExistenteInactivo_RechazaConMensaje()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        await AgregarProductoAsync(db, "Bidón 20L", "Bidón", false, central);
        var controller = Controlador(db);

        await controller.Create(ModeloCrear(norte.Id));

        Assert.Contains("El producto existe, pero está inactivo", (string)controller.TempData["ErrorMessage"]!);
        Assert.Equal(1, await db.ProductosLocal.CountAsync());
    }

    [Fact]
    public async Task Create_NombreExistenteEnTodosLosLocales_Rechaza()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        await AgregarProductoAsync(db, "Bidón 20L", "Bidón", true, central, norte);
        var controller = Controlador(db);

        await controller.Create(ModeloCrear(central.Id, norte.Id));

        Assert.Contains("Este producto ya está registrado en todos los locales seleccionados.",
            (string)controller.TempData["ErrorMessage"]!);
        Assert.Equal(1, await db.Productos.CountAsync());
    }

    [Fact]
    public async Task Create_NombreExistenteSinLocales_SoloPideSeleccionarLocal()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        await AgregarProductoAsync(db, "Bidón 20L", "Bidón", true, central);
        var controller = Controlador(db);

        await controller.Create(ModeloCrear());

        Assert.Equal("Seleccione al menos un local.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Create_NombreExistente_AgregaANuevoLocalYReactivaRelacionInactiva()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Bidón 20L", "Bidón", true, central);
        var relacion = await db.ProductosLocal.SingleAsync();
        relacion.Estado = false;
        await db.SaveChangesAsync();
        var controller = Controlador(db);

        await controller.Create(ModeloCrear(central.Id, norte.Id));

        Assert.Equal("PRODUCTO AGREGADO A LOS LOCALES SELECCIONADOS", controller.TempData["SuccessMessage"]);
        Assert.Equal(1, await db.Productos.CountAsync());
        var relaciones = await db.ProductosLocal.Where(pl => pl.IdProducto == producto.Id).ToListAsync();
        Assert.Equal(2, relaciones.Count);
        Assert.All(relaciones, pl => Assert.True(pl.Estado && pl.EstadoRegistro));
    }

    // ---------- Edit ----------

    [Fact]
    public async Task EditGet_SinAjax_RedirigeEInexistente_NotFound()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();

        Assert.IsType<RedirectToActionResult>(await Controlador(db).Edit(1));
        Assert.IsType<NotFoundResult>(await Controlador(db, ajax: true).Edit(9999));
    }

    [Fact]
    public async Task EditGet_PrecargaDatosYLocalesActivos()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", true, central, norte);
        (await db.ProductosLocal.SingleAsync(pl => pl.IdLocal == norte.Id)).Estado = false;
        await db.SaveChangesAsync();

        var parcial = Assert.IsType<PartialViewResult>(await Controlador(db, ajax: true).Edit(producto.Id));

        Assert.Equal("_Edit", parcial.ViewName);
        var modelo = Assert.IsType<EditarProductoViewModel>(parcial.Model);
        Assert.Equal("Hielo 3kg", modelo.Nombre);
        Assert.Equal("Hielo", modelo.Categoria);
        Assert.Equal(10m, modelo.PrecioVenta);
        Assert.Equal(5m, modelo.Costo);
        Assert.Equal(new[] { central.Id }, modelo.IdLocales);
        Assert.Equal(2, modelo.Locales.Count);
    }

    [Fact]
    public async Task EditGet_CategoriaNula_DevuelveVacio()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Sin categoría", null!, true, central);

        var parcial = Assert.IsType<PartialViewResult>(await Controlador(db, ajax: true).Edit(producto.Id));

        Assert.Equal(string.Empty, Assert.IsType<EditarProductoViewModel>(parcial.Model).Categoria);
    }

    [Fact]
    public async Task EditPost_DatosValidos_ActualizaYSincronizaLocales()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Galón 11L", "Galón", true, central);
        var controller = Controlador(db);

        var resultado = await controller.Edit(new EditarProductoViewModel
        {
            Id = producto.Id,
            Nombre = " Galón 10L ",
            Descripcion = " ",
            Categoria = "Galón",
            PrecioVenta = 15m,
            Costo = 8m,
            IdLocales = new List<long> { norte.Id }
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("PRODUCTO ACTUALIZADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        Assert.Equal("Galón 10L", producto.Nombre);
        Assert.Null(producto.Descripcion);
        Assert.Equal(15m, producto.PrecioVenta);
        Assert.Equal(8m, producto.Costo);
        var relaciones = await db.ProductosLocal.ToListAsync();
        Assert.False(relaciones.Single(pl => pl.IdLocal == central.Id).EstadoRegistro);
        Assert.True(relaciones.Single(pl => pl.IdLocal == norte.Id).Estado);
    }

    [Fact]
    public async Task EditPost_ReactivaRelacionExistenteYConservaDescripcion()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Galón 11L", "Galón", true, central);
        var relacion = await db.ProductosLocal.SingleAsync();
        relacion.Estado = false;
        relacion.EstadoRegistro = false;
        await db.SaveChangesAsync();

        await Controlador(db).Edit(new EditarProductoViewModel
        {
            Id = producto.Id, Nombre = "Galón 11L", Descripcion = " Retornable ", Categoria = "Galón",
            PrecioVenta = 10m, Costo = 5m, IdLocales = new List<long> { central.Id }
        });

        Assert.True(relacion.Estado && relacion.EstadoRegistro);
        Assert.Equal("Retornable", producto.Descripcion);
    }

    [Fact]
    public async Task EditPost_NombreDuplicado_MuestraMensajeYNoActualiza()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", true, central);
        var producto = await AgregarProductoAsync(db, "Hielo 5kg", "Hielo", true, central);
        var controller = Controlador(db, ajax: true);

        var parcial = Assert.IsType<PartialViewResult>(await controller.Edit(new EditarProductoViewModel
        {
            Id = producto.Id, Nombre = "Hielo 3kg", Categoria = "Hielo", PrecioVenta = 1m, Costo = 1m,
            IdLocales = new List<long> { central.Id }
        }));

        Assert.Equal("_Edit", parcial.ViewName);
        Assert.Contains(controller.ModelState[nameof(EditarProductoViewModel.Nombre)]!.Errors,
            e => e.ErrorMessage == "Ya existe un producto con ese nombre.");
        Assert.Equal("Hielo 5kg", (await db.Productos.AsNoTracking().SingleAsync(p => p.Id == producto.Id)).Nombre);
    }

    [Fact]
    public async Task EditPost_SinLocalesOLocalInvalido_RedirigeConError()
    {
        var (db, central, _, inactivo) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", true, central);
        var controller = Controlador(db);

        await controller.Edit(new EditarProductoViewModel
        {
            Id = producto.Id, Nombre = "Hielo 3kg", Categoria = "Hielo", PrecioVenta = 1m, Costo = 1m, IdLocales = null!
        });
        Assert.Contains("Seleccione al menos un local.", (string)controller.TempData["ErrorMessage"]!);

        var otro = Controlador(db);
        await otro.Edit(new EditarProductoViewModel
        {
            Id = producto.Id, Nombre = "Hielo 3kg", Categoria = "Hielo", PrecioVenta = 1m, Costo = 1m,
            IdLocales = new List<long> { inactivo.Id }
        });
        Assert.Contains("Seleccione únicamente locales activos válidos.", (string)otro.TempData["ErrorMessage"]!);
    }

    [Fact]
    public async Task EditPost_ProductoInexistente_NotFound()
    {
        var (db, central, _, _) = await CrearEscenarioAsync();

        Assert.IsType<NotFoundResult>(await Controlador(db).Edit(new EditarProductoViewModel
        {
            Id = 9999, Nombre = "X", Categoria = "Hielo", PrecioVenta = 1m, Costo = 1m,
            IdLocales = new List<long> { central.Id }
        }));
    }

    // ---------- Cambiar estado / eliminar ----------

    [Theory]
    [InlineData(false, "PRODUCTO DESACTIVADO CORRECTAMENTE")]
    [InlineData(true, "PRODUCTO ACTIVADO CORRECTAMENTE")]
    public async Task CambiarEstado_ActualizaYMuestraMensaje(bool estado, string mensaje)
    {
        var (db, central, _, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", !estado, central);
        var controller = Controlador(db);

        Assert.IsType<RedirectToActionResult>(await controller.CambiarEstado(producto.Id, estado));

        Assert.Equal(estado, producto.Estado);
        Assert.Equal(mensaje, controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task CambiarEstadoYDelete_ProductoInexistente_NotFound()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();
        var controller = Controlador(db);

        Assert.IsType<NotFoundResult>(await controller.CambiarEstado(9999, true));
        Assert.IsType<NotFoundResult>(await controller.Delete(9999));
    }

    [Fact]
    public async Task Delete_EliminaProductoYSusRelaciones()
    {
        var (db, central, norte, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Hielo 3kg", "Hielo", true, central, norte);
        var controller = Controlador(db);

        Assert.IsType<RedirectToActionResult>(await controller.Delete(producto.Id));

        Assert.Equal("PRODUCTO ELIMINADO CORRECTAMENTE", controller.TempData["SuccessMessage"]);
        Assert.Empty(await db.Productos.ToListAsync());
        Assert.Empty(await db.ProductosLocal.ToListAsync());
    }

    [Fact]
    public async Task Delete_ProductoSinLocales_EliminaProducto()
    {
        var (db, _, _, _) = await CrearEscenarioAsync();
        var producto = await AgregarProductoAsync(db, "Papel x4", "Papel");

        await Controlador(db).Delete(producto.Id);

        Assert.Empty(await db.Productos.ToListAsync());
    }
}
