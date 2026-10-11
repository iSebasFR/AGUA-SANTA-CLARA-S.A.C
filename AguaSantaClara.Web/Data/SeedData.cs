using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();

        // ============================================================
        // 1. ROLES
        // ============================================================
        var roles = new[]
        {
            new { Codigo = "ADMIN",         Nombre = "Administradora" },
            new { Codigo = "VENDEDORA",     Nombre = "Vendedora" },
            new { Codigo = "GERENTE",       Nombre = "Gerente" }
        };

        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r.Nombre))
            {
                await roleManager.CreateAsync(new Rol
                {
                    Name = r.Nombre,
                    Codigo = r.Codigo,
                    Estado = true,
                    EstadoRegistro = true,
                    FechaCreacion = DateTime.UtcNow
                });
            }
        }

        // ============================================================
        // 2. USUARIOS
        // ============================================================
        var usuariosSeed = new[]
        {
            new { User = "Surabhi",  Nombre = "Surabhi",            Apellido = "Torrejon Alvites", Password = "Surabhi123",  Rol = "Vendedora",      Email = "surabhi@aguasantaclara.pe" },
            new { User = "Kelly",    Nombre = "Kelly",              Apellido = "Yauri Leiva",      Password = "Kelly123",    Rol = "Vendedora",      Email = "kelly@aguasantaclara.pe" },
            new { User = "Karina",   Nombre = "Karina Higinia",     Apellido = "Ramos Cadillo",    Password = "Karina123",   Rol = "Vendedora",      Email = "karina@aguasantaclara.pe" },
            new { User = "Maryorie", Nombre = "Maryorie Titziana",  Apellido = "Alarcon Casihui",  Password = "Maryorie123", Rol = "Administradora", Email = "maryorie@aguasantaclara.pe" },
            new { User = "Renzo",    Nombre = "Renzo",              Apellido = "Aguilar Moreno",   Password = "Renzo123",    Rol = "Gerente",        Email = "renzo@aguasantaclara.pe" }
        };

        foreach (var u in usuariosSeed)
        {
            var existente = await userManager.FindByNameAsync(u.User);
            var rol = await roleManager.FindByNameAsync(u.Rol);
            if (rol == null) continue;

            if (existente == null)
            {
                // ✅ Crear usuario nuevo
                var nuevo = new Usuario
                {
                    UserName = u.User,
                    Email = u.Email,
                    EmailConfirmed = true,
                    Nombres = u.Nombre,
                    Apellidos = u.Apellido,
                    Estado = true,
                    EstadoRegistro = true,
                    FechaCreacion = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString("D"),
                    IdRol = rol.Id
                };

                var result = await userManager.CreateAsync(nuevo, u.Password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(nuevo, u.Rol);
                else
                    Console.WriteLine($"❌ Error creando usuario {u.User}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            else
            {
                // ✅ Usuario ya existe → forzar actualización de password y datos
                existente.Nombres = u.Nombre;
                existente.Apellidos = u.Apellido;
                existente.Email = u.Email;
                existente.EmailConfirmed = true;
                existente.Estado = true;
                existente.EstadoRegistro = true;
                existente.IdRol = rol.Id;

                // Regenerar hash de contraseña
                existente.PasswordHash = userManager.PasswordHasher.HashPassword(existente, u.Password);
                existente.SecurityStamp = Guid.NewGuid().ToString("D");

                var update = await userManager.UpdateAsync(existente);
                if (!update.Succeeded)
                    Console.WriteLine($"❌ Error actualizando usuario {u.User}: {string.Join(", ", update.Errors.Select(e => e.Description))}");

                // Asegurar que tenga el rol asignado
                var rolesActuales = await userManager.GetRolesAsync(existente);
                if (!rolesActuales.Contains(u.Rol))
                    await userManager.AddToRoleAsync(existente, u.Rol);
            }
        }

        // ============================================================
        // 3. RESTO DE SEED
        // ============================================================
        var db = services.GetRequiredService<AppDbContext>();

        await SeedMetodosPagoAsync(db);
        await SeedLocalesAsync(db);
        await SeedRepartidoresAsync(db);
        await SeedProductosAsync(db);
        await SeedInsumosAsync(db);
        await SeedClientesAsync(db);
    }

    // ============================================================
    // MÉTODOS DE PAGO
    // ============================================================
    private static async Task SeedMetodosPagoAsync(AppDbContext db)
    {
        var metodos = new[] { "Efectivo", "Yape", "Transferencia Bancaria" };
        foreach (var nombre in metodos)
        {
            if (!await db.MetodosPago.AnyAsync(m => m.Nombre == nombre))
                db.MetodosPago.Add(new MetodoPago { Nombre = nombre });
        }
        await db.SaveChangesAsync();
    }

    // ============================================================
    // LOCALES
    // ============================================================
    private static async Task SeedLocalesAsync(AppDbContext db)
    {
        var locales = new[] { "Santa Rosa", "Niño Jesús", "Apurímac" };

        foreach (var nombre in locales)
        {
            if (!await db.Locales.AnyAsync(l => l.Nombre == nombre))
                db.Locales.Add(new Local { Nombre = nombre });
        }

        await db.SaveChangesAsync();
    }

    // ============================================================
    // REPARTIDORES
    // ============================================================
    private static async Task SeedRepartidoresAsync(AppDbContext db)
    {
        var repartidores = new[]
        {
            new { Nombre = "Jose Luis",  Celular = "51991654945" },
            new { Nombre = "Carlos",     Celular = "51963854929" },
            new { Nombre = "Miguel",     Celular = "51991093927" },
            new { Nombre = "Juan",       Celular = "51987654321" },
            new { Nombre = "David",      Celular = "51912345678" }
        };

        foreach (var item in repartidores)
        {
            var existente = await db.Repartidores
                .FirstOrDefaultAsync(r => r.Celular == item.Celular);

            if (existente == null)
            {
                db.Repartidores.Add(new Repartidor
                {
                    Nombre = item.Nombre,
                    Celular = item.Celular,
                    Estado = true,
                    EstadoRegistro = true
                });
            }
            else
            {
                existente.Nombre = item.Nombre;
                existente.Estado = true;
                existente.EstadoRegistro = true;
            }
        }

        await db.SaveChangesAsync();
    }

    // ============================================================
    // PRODUCTOS + STOCK INICIAL POR LOCAL
    // ============================================================
    private static async Task SeedProductosAsync(AppDbContext db)
    {
        // Nombre, Categoría, PrecioVenta, Costo, Descripción, EsRetornable, Locales (nombres), StockPorLocal
        var productos = new[]
        {
            new {
                Nombre = "Bidón Agua Clara 20L",
                Categoria = "Bidón",
                Precio = 25.00m,
                Costo = 10.00m,
                Descripcion = "Bidón de agua de 20 litros, envase retornable.",
                Retornable = true,
                Locales = new[] { "Santa Rosa" },
                Stocks = new[] { 50 }
            },
            new {
                Nombre = "Bidón El Real 20L",
                Categoria = "Bidón",
                Precio = 25.00m,
                Costo = 10.00m,
                Descripcion = "Bidón de agua de 20 litros, envase retornable.",
                Retornable = true,
                Locales = new[] { "Santa Rosa" },
                Stocks = new[] { 40 }
            },
            new {
                Nombre = "Galón Agua Clara 11L",
                Categoria = "Galón",
                Precio = 5.00m,
                Costo = 1.50m,
                Descripcion = "Galón de agua de 11 litros.",
                Retornable = true,
                Locales = new[] { "Santa Rosa", "Niño Jesús" },
                Stocks = new[] { 80, 60 }
            },
            new {
                Nombre = "Galón El Real 11L",
                Categoria = "Galón",
                Precio = 5.00m,
                Costo = 1.50m,
                Descripcion = "Galón de agua de 11 litros.",
                Retornable = true,
                Locales = new[] { "Santa Rosa" },
                Stocks = new[] { 70 }
            },
            new {
                Nombre = "Galón Agua Clara 8.8L",
                Categoria = "Galón",
                Precio = 3.50m,
                Costo = 0.50m,
                Descripcion = "Galón de agua de 8.8 litros.",
                Retornable = true,
                Locales = new[] { "Santa Rosa" },
                Stocks = new[] { 90 }
            },
            new {
                Nombre = "Papel Higiénico Molly",
                Categoria = "Papel",
                Precio = 8.00m,
                Costo = 3.00m,
                Descripcion = "Paquete de 6 unidades grandes de papel higiénico.",
                Retornable = false,
                Locales = new[] { "Apurímac" },
                Stocks = new[] { 60 }
            },
            new {
                Nombre = "Bolsa de Hielo Blue 1.5kg",
                Categoria = "Hielo",
                Precio = 5.00m,
                Costo = 1.50m,
                Descripcion = "Bolsa de hielo de 1.5 kilogramos.",
                Retornable = false,
                Locales = new[] { "Niño Jesús" },
                Stocks = new[] { 80 }
            }
        };

        foreach (var p in productos)
        {
            var existente = await db.Productos
                .FirstOrDefaultAsync(x => x.Nombre == p.Nombre && x.EstadoRegistro);

            Producto producto;

            if (existente == null)
            {
                producto = new Producto
                {
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    Categoria = p.Categoria,
                    PrecioVenta = p.Precio,
                    Costo = p.Costo,
                    EsRetornable = p.Retornable,
                    StockActual = 0,
                    StockMinimo = 0,
                    Estado = true,
                    EstadoRegistro = true
                };
                db.Productos.Add(producto);
                await db.SaveChangesAsync();
            }
            else
            {
                producto = existente;
            }

            // Asignar a locales con su stock
            for (int i = 0; i < p.Locales.Length; i++)
            {
                var nombreLocal = p.Locales[i];
                var stockInicial = p.Stocks[i];

                var local = await db.Locales.FirstOrDefaultAsync(l => l.Nombre == nombreLocal);
                if (local == null) continue;

                var yaExiste = await db.ProductosLocal
                    .AnyAsync(pl => pl.IdProducto == producto.Id && pl.IdLocal == local.Id);

                if (!yaExiste)
                {
                    db.ProductosLocal.Add(new ProductoLocal
                    {
                        IdProducto = producto.Id,
                        IdLocal = local.Id,
                        Stock = stockInicial,
                        Estado = true,
                        EstadoRegistro = true
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    // ============================================================
    // INSUMOS
    // ============================================================
    private static async Task SeedInsumosAsync(AppDbContext db)
    {
        // Nombre, LíneaProducto, Costo, Descripción, StockInicial, StockMinimo
        var insumos = new[]
        {
            new { Nombre = "Tapas de galones Agua Clara", Linea = "agua",  Costo = 250.00m, Desc = "Tapa estándar para galones de la marca Agua Clara. Compra por millar.", Stock = 1000, Min = 200 },
            new { Nombre = "Tapas de galones El Real",    Linea = "agua",  Costo = 250.00m, Desc = "Tapa estándar para galones de la marca El Real. Compra por millar.",    Stock = 1000, Min = 200 },
            new { Nombre = "Tapas de bidones Agua Clara", Linea = "agua",  Costo = 300.00m, Desc = "Tapa de seguridad para bidones de la marca Agua Clara. Compra por millar.", Stock = 1000, Min = 200 },
            new { Nombre = "Etiquetas 11L Agua Clara",    Linea = "agua",  Costo = 160.00m, Desc = "Etiqueta autoadhesiva para galones de 11 litros Agua Clara. Por millar.",  Stock = 1000, Min = 200 },
            new { Nombre = "Etiquetas 8.8L Agua Clara",   Linea = "agua",  Costo = 140.00m, Desc = "Etiqueta autoadhesiva para galones de 8.8 litros Agua Clara. Por millar.", Stock = 1000, Min = 200 },
            new { Nombre = "Etiquetas 11L El Real",       Linea = "agua",  Costo = 160.00m, Desc = "Etiqueta autoadhesiva para galones de 11 litros El Real. Por millar.",     Stock = 1000, Min = 200 },
            new { Nombre = "Precintos de seguridad",      Linea = "agua",  Costo = 240.00m, Desc = "Precinto de seguridad para sellado de envases. Compra por millar.",         Stock = 1000, Min = 200 },
            new { Nombre = "Bolsas para hielo",           Linea = "hielo", Costo = 70.00m,  Desc = "Bolsa transparente para envasado de hielo de 1.5 kg. Por millar.",        Stock = 1000, Min = 200 },
            new { Nombre = "Bolsas para papel higiénico", Linea = "papel", Costo = 130.00m, Desc = "Bolsa para empaque de paquete de papel higiénico. Por millar.",            Stock = 1000, Min = 200 }
        };

        foreach (var i in insumos)
        {
            var existente = await db.Insumos
                .FirstOrDefaultAsync(x => x.Nombre == i.Nombre && x.EstadoRegistro);

            if (existente == null)
            {
                db.Insumos.Add(new Insumo
                {
                    Nombre = i.Nombre,
                    Descripcion = i.Desc,
                    LineaProducto = i.Linea,
                    Costo = i.Costo,
                    StockActual = i.Stock,
                    StockMinimo = i.Min,
                    Estado = true,
                    EstadoRegistro = true
                });
            }
            else
            {
                existente.Descripcion = i.Desc;
                existente.LineaProducto = i.Linea;
                existente.Costo = i.Costo;
                existente.Estado = true;
                existente.EstadoRegistro = true;
            }
        }

        await db.SaveChangesAsync();
    }

    // ============================================================
    // CLIENTES + DIRECCIONES
    // ============================================================
    private static async Task SeedClientesAsync(AppDbContext db)
    {
        var clientesSeed = new[]
        {
            new {
                Nombre = "María Elena Quispe Huamán",
                Telefono = "987654321",
                Dni = "45781236",
                Direcciones = new[] {
                    new { Direccion = "Av. Los Álamos 145, Dpto. 302", Ciudad = "Lima", Referencia = "Frente al parque central", Url = "https://maps.google.com/?q=-12.0464,-77.0428" }
                }
            },
            new {
                Nombre = "Carlos Alberto Mendoza Ríos",
                Telefono = "912345678",
                Dni = "41258796",
                Direcciones = new[] {
                    new { Direccion = "Calle Las Begonias 220", Ciudad = "Lima", Referencia = "Portón negro al lado de la bodega", Url = (string?)null },
                    new { Direccion = "Jr. Puno 155, Int. 4", Ciudad = "Lima", Referencia = "Edificio azul", Url = (string?)null }
                }
            },
            new {
                Nombre = "Rosa Amelia Fernández Castro",
                Telefono = "998877665",
                Dni = "09876543",
                Direcciones = new[]
                {
                    new { Direccion = "Av. Arequipa 2340, Dpto. 801", Ciudad = "Lima", Referencia = "Edificio Miraflores", Url = (string?)null },
                    new { Direccion = "Calle Berlín 345", Ciudad = "Lima", Referencia = "Casa con reja verde", Url = (string?)null },
                    new { Direccion = "Av. Brasil 1180, Of. 205", Ciudad = "Lima", Referencia = "Centro comercial", Url = (string?)null }
                }
            },
            new {
                Nombre = "José Antonio Ramírez Torres",
                Telefono = "955443322",
                Dni = (string?)null,
                Direcciones = new[] {
                    new { Direccion = "Av. Universitaria 1850", Ciudad = "Lima", Referencia = "Al costado del grifo Primax", Url = (string?)null }
                }
            },
            new {
                Nombre = "Lucía Beatriz Salazar Vega",
                Telefono = "944332211",
                Dni = "48756231",
                Direcciones = new[] {
                    new { Direccion = "Calle Los Nogales 78", Ciudad = "Lima", Referencia = "Casa blanca con jardín", Url = (string?)null },
                    new { Direccion = "Av. La Marina 2300, Tienda 12", Ciudad = "Lima", Referencia = "Mercado central", Url = (string?)null }
                }
            },
            new {
                Nombre = "Pedro Pablo Gutiérrez Sánchez",
                Telefono = "933221100",
                Dni = "41234567",
                Direcciones = new[] {
                    new { Direccion = "Jr. Cusco 560, Piso 3", Ciudad = "Lima", Referencia = "Edificio amarillo", Url = (string?)null }
                }
            },
            new {
                Nombre = "Ana Sofía Rojas Paredes",
                Telefono = "966778899",
                Dni = "47851236",
                Direcciones = new[]
                {
                    new { Direccion = "Av. Ejército 1230, Dpto. 502", Ciudad = "Lima", Referencia = "Frente al banco BCP", Url = (string?)null },
                    new { Direccion = "Calle Comercio 245", Ciudad = "Lima", Referencia = "Tienda de abarrotes", Url = (string?)null }
                }
            },
            new {
                Nombre = "Jorge Luis Vásquez Flores",
                Telefono = "977889900",
                Dni = (string?)null,
                Direcciones = new[] {
                    new { Direccion = "Av. Grau 780", Ciudad = "Lima", Referencia = "Esquina con jirón Amazonas", Url = (string?)null }
                }
            },
            new {
                Nombre = "Elena Margarita Chávez Lozano",
                Telefono = "988990011",
                Dni = "41258963",
                Direcciones = new[]
                {
                    new { Direccion = "Av. Venezuela 2150, Dpto. 203", Ciudad = "Lima", Referencia = "Edificio San Felipe", Url = (string?)null },
                    new { Direccion = "Calle Ica 340", Ciudad = "Lima", Referencia = "Casa con portón blanco", Url = (string?)null },
                    new { Direccion = "Av. Colonial 980, Local 5", Ciudad = "Lima", Referencia = "Bodega", Url = (string?)null }
                }
            },
            new {
                Nombre = "Ricardo Martín Espinoza Aguilar",
                Telefono = "922334455",
                Dni = "45678912",
                Direcciones = new[] {
                    new { Direccion = "Calle Manco Cápac 512", Ciudad = "Lima", Referencia = "Al lado de la farmacia", Url = (string?)null }
                }
            }
        };

        foreach (var c in clientesSeed)
        {
            var existente = await db.Clientes
                .FirstOrDefaultAsync(x => x.Nombre == c.Nombre && x.EstadoRegistro);

            if (existente != null) continue;

            var cliente = new Cliente
            {
                Nombre = c.Nombre,
                Telefono = c.Telefono,
                Dni = c.Dni,
                Estado = true,
                EstadoRegistro = true,
                SaldoBidones = 0,
                DeudaTotal = 0
            };

            for (int i = 0; i < c.Direcciones.Length; i++)
            {
                var d = c.Direcciones[i];
                cliente.Direcciones.Add(new DireccionCliente
                {
                    Direccion = d.Direccion,
                    Ciudad = d.Ciudad,
                    Referencia = d.Referencia,
                    UrlUbicacion = d.Url,
                    Principal = (i == 0),
                    Estado = true,
                    EstadoRegistro = true
                });
            }

            db.Clientes.Add(cliente);
        }

        await db.SaveChangesAsync();
    }
}