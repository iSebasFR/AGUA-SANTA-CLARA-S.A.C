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

        // 2. Obtener la entidad del Rol Gerente
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

        var db = services.GetRequiredService<AppDbContext>();
        await SeedMetodosPagoAsync(db);
        await SeedPedidosAsync(db);
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
        // ============================================================
        // ❌ PRODUCTOS PREDETERMINADOS ELIMINADOS
        // Los productos se registran manualmente desde el módulo Catálogo.
        // ============================================================

        // ============================================================
        // LOCALES (se mantienen por si se usan en el sistema)
        // ============================================================
        var localesIniciales = new[]
        {
            "Santa Rosa",
            "Niño Jesús",
            "Apurímac"
        };

        foreach (var nombre in localesIniciales)
        {
            if (!await db.Locales.AnyAsync(l => l.Nombre == nombre))
                db.Locales.Add(new Local { Nombre = nombre });
        }

        await db.SaveChangesAsync();

        // ============================================================
        // REPARTIDORES (se mantienen para pruebas de pedidos)
        // ============================================================
        var repartidores = new[]
        {
            new { Nombre = "Carlos Ramos", Celular = "51991093927" },
            new { Nombre = "Luis Paredes", Celular = "51963854929" },
            new { Nombre = "Jorge Salas", Celular = "51991654945" }
        };

        var repartidoresExistentes = await db.Repartidores.ToListAsync();
        foreach (var item in repartidores)
        {
            var coincidencias = repartidoresExistentes
                .Where(r => string.Equals(r.Nombre.Trim(), item.Nombre, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Id)
                .ToList();

            var repartidor = coincidencias.FirstOrDefault(r => r.Celular == item.Celular)
                ?? coincidencias.FirstOrDefault(r => r.EstadoRegistro)
                ?? coincidencias.FirstOrDefault();

            if (repartidor == null)
            {
                repartidor = new Repartidor { Nombre = item.Nombre, Celular = item.Celular };
                db.Repartidores.Add(repartidor);
                repartidoresExistentes.Add(repartidor);
            }
            else
            {
                repartidor.Nombre = item.Nombre;
                repartidor.Celular = item.Celular;
                repartidor.Estado = true;
                repartidor.EstadoRegistro = true;
            }

            foreach (var duplicado in coincidencias.Where(r => r.Id != repartidor.Id))
            {
                duplicado.Estado = false;
                duplicado.EstadoRegistro = false;
            }
        }

        await db.SaveChangesAsync();
    }
}