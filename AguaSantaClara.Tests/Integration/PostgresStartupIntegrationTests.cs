
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using AguaSantaClara.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AguaSantaClara.Tests.Integration;

public class PostgresStartupIntegrationTests
{
    [Fact]
    public async Task TestingPostgres_DebeIniciarYResponderLogin()
    {
        // Exigir una conexión de prueba explícita.
        var connectionString = Environment.GetEnvironmentVariable(
            "HT13_TEST_DB_CONNECTION");

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        // Iniciar la aplicación con PostgreSQL de pruebas.
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseEnvironment("TestingPostgres"));

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        // La aplicación debe haber ejecutado las migraciones
        // antes de responder esta solicitud.
        using var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Comprobar que PostgreSQL está disponible.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.Database.CanConnectAsync());

        // Comprobar que todas las migraciones están aplicadas.
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();

        Assert.Empty(pendingMigrations);
    }
}
