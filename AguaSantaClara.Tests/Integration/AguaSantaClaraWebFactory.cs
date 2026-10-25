using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AguaSantaClara.Tests.Integration;

public class AguaSantaClaraWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Activar el entorno de pruebas.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Base de datos temporal y aislada.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(
                    $"AguaSantaClara_Test_{Guid.NewGuid()}"));
        });
    }
}