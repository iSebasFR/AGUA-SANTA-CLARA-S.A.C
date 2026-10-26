
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AguaSantaClara.Tests.Integration;

public class AccountLoginIntegrationTests
{
    [Fact]
    public async Task Login_Get_DebeResponderCorrectamente()
    {
        // Arrange: crear la aplicación en el entorno Testing.
        using var factory = new AguaSantaClaraWebFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        // Act: simular una petición HTTP a la página de Login.
        var response = await client.GetAsync("/Account/Login");

        // Assert: verificar que la página responde con HTTP 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
