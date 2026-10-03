using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AguaSantaClara.Web.Models;

namespace AguaSantaClara.Web.Controllers;

public class HomeController : Controller
{
    [Authorize(Roles = "Administradora,Gerente,Vendedora")]
    public IActionResult Index()
    {
        return View();
    }

    [Authorize(Roles = "Administradora,Gerente,Vendedora")]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}