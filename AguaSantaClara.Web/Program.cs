using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Alertas;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AguaSantaClara.Web.Middleware;
using AguaSantaClara.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configura ConnectionStrings__DefaultConnection o DATABASE_URL antes de iniciar la aplicación.");
}

if (Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri)
    && (databaseUri.Scheme == "postgres" || databaseUri.Scheme == "postgresql"))
{
    var credentials = databaseUri.UserInfo.Split(':', 2);
    connectionString = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
        Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(credentials[0]),
        Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
        SslMode = Npgsql.SslMode.Require
    }.ConnectionString;
}

// 1. EF Core + PostgreSQL (con convención snake_case para PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// 2. ASP.NET Core Identity con entidades personalizadas
builder.Services.AddIdentity<Usuario, Rol>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = false; // <-- CAMBIO: ya no usamos Email
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Configuración de Cookies de autenticación para redireccionar al Login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true; // <-- CAMBIO: renueva la cookie mientras el usuario está activo
});

// 4. CORRECCIÓN: Desactivar la revalidación del SecurityStamp en cada request
// Esto evita que al crear/editar un usuario se invalide la cookie del Gerente actual
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromHours(8); // Revalida cada 8 horas (o usa TimeSpan.Zero para desactivar)
});

builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IAutoventaService, AutoventaService>(); // HU-14 T10
builder.Services.AddScoped<IAlertasService, AlertasService>();
builder.Services.AddOptions<AlertasOptions>()
    .BindConfiguration(AlertasOptions.SectionName)
    .Validate(options => options.HoraEjecucion is >= 0 and <= 23, "Alertas:HoraEjecucion debe estar entre 0 y 23.")
    .Validate(options => options.MinutoEjecucion is >= 0 and <= 59, "Alertas:MinutoEjecucion debe estar entre 0 y 59.")
    .Validate(options => options.UtcOffsetHoras is >= -12 and <= 14, "Alertas:UtcOffsetHoras debe estar entre -12 y 14.")
    .Validate(options => options.FrecuenciaDias > 0, "Alertas:FrecuenciaDias debe ser mayor que cero.")
    .Validate(options => options.IntervaloRevisionSegundos > 0, "Alertas:IntervaloRevisionSegundos debe ser mayor que cero.")
    .ValidateOnStart();
builder.Services.AddHostedService<AlertasBackgroundService>();

builder.Services.AddControllersWithViews();

// API REST + Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "API Agua Santa Clara",
        Version = "v1",
        Description = "API REST del Sistema de Pedidos y Distribución"
    });
});

var app = builder.Build();

// 5. Migración automática + Seed DENTRO del mismo scope al arrancar
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        await SeedData.InitializeAsync(services, builder.Configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones o sembrar la base de datos.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Errores de API/AJAX como JSON; las páginas MVC siguen usando /Home/Error
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

// Swagger (solo en desarrollo)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Agua Santa Clara v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();