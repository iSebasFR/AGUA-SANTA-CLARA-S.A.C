using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

public class ProductosController : Controller
{
    public IActionResult Index() => View();
}