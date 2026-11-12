using System.Security.Claims;
using AguaSantaClara.Web.Data;
using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AguaSantaClara.Tests.Soporte;

public sealed class TempDataEnMemoria : ITempDataProvider
{
    private IDictionary<string, object> _datos = new Dictionary<string, object>();
    public IDictionary<string, object> LoadTempData(HttpContext context) => _datos;
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _datos = values;
}

public sealed class AutenticacionFalsa : IAuthenticationService
{
    public ClaimsPrincipal? UsuarioFirmado { get; private set; }
    public bool SesionCerrada { get; private set; }

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        UsuarioFirmado = principal;
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SesionCerrada = true;
        return Task.CompletedTask;
    }
}

public sealed class UrlHelperFalso : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();
    public string? Action(UrlActionContext actionContext) => null;
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//");
    public string? Link(string? routeName, object? values) => null;
    public string? RouteUrl(UrlRouteContext routeContext) => null;
}

public sealed class UserManagerConFallas : UserManager<Usuario>
{
    public bool FallarCrear { get; set; }
    public bool FallarAsignarRol { get; set; }
    public bool FallarActualizar { get; set; }
    public bool FallarEliminar { get; set; }
    public bool EliminarFueLlamado { get; private set; }

    public static readonly IdentityError ErrorSimulado = new() { Code = "Simulado", Description = "Fallo simulado de Identity." };

    public UserManagerConFallas(IServiceProvider sp) : base(
        sp.GetRequiredService<IUserStore<Usuario>>(),
        sp.GetRequiredService<IOptions<IdentityOptions>>(),
        sp.GetRequiredService<IPasswordHasher<Usuario>>(),
        sp.GetServices<IUserValidator<Usuario>>(),
        sp.GetServices<IPasswordValidator<Usuario>>(),
        sp.GetRequiredService<ILookupNormalizer>(),
        sp.GetRequiredService<IdentityErrorDescriber>(),
        sp,
        sp.GetRequiredService<ILogger<UserManager<Usuario>>>())
    {
        RegisterTokenProvider(TokenOptions.DefaultProvider,
            sp.GetRequiredService<DataProtectorTokenProvider<Usuario>>());
    }

    public override Task<IdentityResult> CreateAsync(Usuario user, string password) =>
        FallarCrear ? Task.FromResult(IdentityResult.Failed(ErrorSimulado)) : base.CreateAsync(user, password);

    public override Task<IdentityResult> AddToRoleAsync(Usuario user, string role) =>
        FallarAsignarRol ? Task.FromResult(IdentityResult.Failed(ErrorSimulado)) : base.AddToRoleAsync(user, role);

    public override Task<IdentityResult> UpdateAsync(Usuario user) =>
        FallarActualizar ? Task.FromResult(IdentityResult.Failed(ErrorSimulado)) : base.UpdateAsync(user);

    public override Task<IdentityResult> DeleteAsync(Usuario user)
    {
        EliminarFueLlamado = true;
        return FallarEliminar ? Task.FromResult(IdentityResult.Failed(ErrorSimulado)) : base.DeleteAsync(user);
    }
}

public sealed class EntornoIdentity : IDisposable
{
    private readonly ServiceProvider _raiz;
    private readonly IServiceScope _scope;

    public AppDbContext Db { get; }
    public UserManagerConFallas UserManager { get; }
    public IPasswordHasher<Usuario> Hasher { get; }
    public Rol RolGerente { get; private set; } = null!;
    public Rol RolAdmin { get; private set; } = null!;
    public Rol RolVendedora { get; private set; } = null!;

    private EntornoIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var nombreBd = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nombreBd));
        services.AddIdentity<Usuario, Rol>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        _raiz = services.BuildServiceProvider();
        _scope = _raiz.CreateScope();
        Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        UserManager = new UserManagerConFallas(_scope.ServiceProvider);
        Hasher = _scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
    }

    public static async Task<EntornoIdentity> CrearAsync()
    {
        var entorno = new EntornoIdentity();
        entorno.RolGerente = new Rol { Name = "Gerente", NormalizedName = "GERENTE", Codigo = "GERENTE" };
        entorno.RolAdmin = new Rol { Name = "Administradora", NormalizedName = "ADMINISTRADORA", Codigo = "ADMIN" };
        entorno.RolVendedora = new Rol { Name = "Vendedora", NormalizedName = "VENDEDORA", Codigo = "VENDEDORA" };
        entorno.Db.Roles.AddRange(entorno.RolGerente, entorno.RolAdmin, entorno.RolVendedora);
        await entorno.Db.SaveChangesAsync();
        return entorno;
    }

    public async Task<Usuario> CrearUsuarioAsync(string userName, Rol rol, string password = "Clave1234",
        bool estado = true, string nombres = "Nombre", string apellidos = "Apellido")
    {
        var usuario = new Usuario
        {
            UserName = userName,
            Nombres = nombres,
            Apellidos = apellidos,
            IdRol = rol.Id,
            Estado = estado,
            SecurityStamp = Guid.NewGuid().ToString("D")
        };
        var resultado = await UserManager.CreateAsync(usuario, password);
        Assert.True(resultado.Succeeded, string.Join(" | ", resultado.Errors.Select(e => e.Description)));
        await UserManager.AddToRoleAsync(usuario, rol.Name!);
        return usuario;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _raiz.Dispose();
    }
}

public static class ControladorFactory
{
    public static T Preparar<T>(T controller, bool ajax = false, IAuthenticationService? auth = null,
        string rol = "Gerente") where T : Controller
    {
        var servicios = new ServiceCollection();
        if (auth != null) servicios.AddSingleton(auth);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = servicios.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, rol) }, "test"))
        };
        if (ajax) httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TempDataEnMemoria());
        controller.Url = new UrlHelperFalso();
        return controller;
    }

    public static void ValidarModelo(Controller controller, object modelo)
    {
        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            modelo, new System.ComponentModel.DataAnnotations.ValidationContext(modelo), resultados, true);
        foreach (var r in resultados)
            foreach (var miembro in r.MemberNames.DefaultIfEmpty(string.Empty))
                controller.ModelState.AddModelError(miembro, r.ErrorMessage ?? string.Empty);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public const string Variable = "HT13_TEST_DB_CONNECTION";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Pendiente: requiere PostgreSQL de pruebas ({Variable}) porque usa EF.Functions.ILike.";
    }

    public static AppDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable(Variable)!)
            .Options);
}
