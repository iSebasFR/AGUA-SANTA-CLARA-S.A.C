using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace AguaSantaClara.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();

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
                    Codigo = r.Codigo
                });
            }
        }
    }
}