using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

public class TestController : Controller
{
    private readonly AppDbContext _context;
    public TestController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Conexion()
    {
        try
        {
            return Json(new
            {
                status = "OK",
                mensaje = "Conexión a PostgreSQL exitosa",
                roles = await _context.Roles.CountAsync(),
                productos = await _context.Productos.CountAsync(),
                insumos = await _context.Insumos.CountAsync(),
                clientes = await _context.Clientes.CountAsync()
            });
        }
        catch (Exception ex)
        {
            return Json(new { status = "ERROR", mensaje = ex.Message });
        }
    }
}