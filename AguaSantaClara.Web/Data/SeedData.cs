using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;

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
                Nombres = "German",
                Apellidos = "Fernandez",
                Estado = true,
                EstadoRegistro = true,
                FechaCreacion = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("D"),
                IdRol = rolGerente.Id // Si tu modelo Rol usa 'IdRol' en vez de 'Id', cámbialo a rolGerente.IdRol
            };

            var result = await userManager.CreateAsync(gerenteUser, "Gerente123*");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(gerenteUser, "Gerente");
            }
        }
    }
}