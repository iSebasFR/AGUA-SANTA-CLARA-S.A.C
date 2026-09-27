using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers.Api;

[ApiController]
[Route("api/prueba")]
public class PruebaApiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "OK",
            mensaje = "API REST de Agua Santa Clara funcionando correctamente",
            fecha = DateTime.UtcNow
        });
    }
}