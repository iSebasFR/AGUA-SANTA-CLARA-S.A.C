using AguaSantaClara.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Controllers;

// Diagnóstico de conexión: expone conteos de la BD, solo para el Gerente
[Authorize(Roles = "Gerente")]
public class TestController : Controller
{
    private readonly AppDbContext _context;

    public TestController(AppDbContext context) => _context = context;

    public async Task<IActionResult> Conexion()
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
}