using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();

        // 1. Crear Roles si no existen
        var roles = new[]
        {
            new { Codigo = "ADMIN", Nombre = "Administradora" },
            new { Codigo = "VENDEDORA", Nombre = "Vendedora" },
            new { Codigo = "GERENTE", Nombre = "Gerente" }
        };

        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r.Nombre))
            {
                await roleManager.CreateAsync(new Rol
                {
                    Name = r.Nombre,
                    Codigo = r.Codigo,
                    Estado = true,
                    EstadoRegistro = true,
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }

        // 2. Obtener la entidad del Rol Gerente (aquí se declara la variable)
        var rolGerente = await roleManager.FindByNameAsync("Gerente");

        if (rolGerente == null)
            return;

        // 3. Crear Usuario Gerente Inicial
        string usernameGerente = "gerente";
        var gerenteExistente = await userManager.FindByNameAsync(usernameGerente);

        if (gerenteExistente == null)
        {
            var gerenteUser = new Usuario
            {
                UserName = usernameGerente,
                Email = "gerente@aguasantaclara.pe",
                EmailConfirmed = true,
                Nombres = "Renzo",
                Apellidos = "Aguilar",
                Estado = true,
                EstadoRegistro = true,
                FechaCreacion = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("D"),
                IdRol = rolGerente.Id
            };

            var result = await userManager.CreateAsync(gerenteUser, "Gerente123*");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(gerenteUser, "Gerente");
            }
        }

        await SeedPedidosAsync(services.GetRequiredService<AppDbContext>());
    }
    private static async Task SeedMetodosPagoAsync(AppDbContext db)
    {
        var metodos = new[] { "Efectivo", "Yape", "Transferencia Bancaria" };
        foreach (var nombre in metodos)
        {
            if (!await db.MetodosPago.AnyAsync(m => m.Nombre == nombre))
                db.MetodosPago.Add(new MetodoPago { Nombre = nombre });
        }
        await db.SaveChangesAsync();
    }
    private static async Task SeedPedidosAsync(AppDbContext db)
    {
        var catalogo = new[]
        {
            new { Nombre = "Galones 11L", Precio = 12.00m, Costo = 7.00m },
            new { Nombre = "Galones 8L", Precio = 9.00m, Costo = 5.00m },
            new { Nombre = "Bidones 20L", Precio = 10.00m, Costo = 6.00m },
            new { Nombre = "Bolsas de Hielo", Precio = 5.00m, Costo = 2.50m },
            new { Nombre = "Papel Higiénico", Precio = 25.00m, Costo = 18.00m }
        };

        var stockPorLocal = new[]
        {
            new { Local = "Santa Rosa", Producto = "Galones 11L", Stock = 120 },
            new { Local = "Santa Rosa", Producto = "Galones 8L", Stock = 80 },
            new { Local = "Santa Rosa", Producto = "Bidones 20L", Stock = 150 },
            new { Local = "Niño Jesús", Producto = "Galones 11L", Stock = 90 },
            new { Local = "Niño Jesús", Producto = "Bolsas de Hielo", Stock = 3 },
            new { Local = "Apurímac", Producto = "Papel Higiénico", Stock = 60 }
        };

        foreach (var item in catalogo)
        {
            if (await db.Productos.AnyAsync(p => p.Nombre == item.Nombre))
                continue;

            db.Productos.Add(new Producto
            {
                Nombre = item.Nombre,
                PrecioVenta = item.Precio,
                Costo = item.Costo,
                StockActual = stockPorLocal.Where(s => s.Producto == item.Nombre).Sum(s => s.Stock),
                StockMinimo = 5
            });
        }

        foreach (var nombre in stockPorLocal.Select(s => s.Local).Distinct())
        {
            if (!await db.Locales.AnyAsync(l => l.Nombre == nombre))
                db.Locales.Add(new Local { Nombre = nombre });
        }

        await db.SaveChangesAsync();

        foreach (var item in stockPorLocal)
        {
            var local = await db.Locales.FirstAsync(l => l.Nombre == item.Local);
            var producto = await db.Productos.FirstAsync(p => p.Nombre == item.Producto);

            if (await db.ProductosLocal.AnyAsync(p => p.IdLocal == local.Id && p.IdProducto == producto.Id))
                continue;

            db.ProductosLocal.Add(new ProductoLocal
            {
                IdLocal = local.Id,
                IdProducto = producto.Id,
                Stock = item.Stock
            });
        }

        var repartidores = new[]
        {
            new { Nombre = "Carlos Ramos", Celular = "51991093927" },
            new { Nombre = "Luis Paredes", Celular = "51987654321" },
            new { Nombre = "Jorge Salas", Celular = "51987654323" }
        };

        foreach (var item in repartidores)
        {
            if (!await db.Repartidores.AnyAsync(r => r.Celular == item.Celular))
                db.Repartidores.Add(new Repartidor { Nombre = item.Nombre, Celular = item.Celular });
        }

        await db.SaveChangesAsync();
    }
}
