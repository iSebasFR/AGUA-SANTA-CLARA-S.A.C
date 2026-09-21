using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

public class ClientesController : Controller
{
    public IActionResult Index() => View();
}