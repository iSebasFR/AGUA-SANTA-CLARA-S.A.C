
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Gerente")]
public class IndicadoresController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
