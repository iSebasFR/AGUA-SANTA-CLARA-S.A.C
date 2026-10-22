using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

[Authorize(Roles = "Vendedora")]
public class CierreCajaController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
