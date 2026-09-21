using Microsoft.AspNetCore.Mvc;

namespace AguaSantaClara.Web.Controllers;

public class UsuariosController : Controller
{
    public IActionResult Index() => View();
}