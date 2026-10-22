namespace AguaSantaClara.Web.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Ocurrió una excepción no controlada al procesar {Metodo} {Ruta}",
                context.Request.Method,
                context.Request.Path);

            // Páginas MVC: se deja subir la excepción para que UseExceptionHandler muestre /Home/Error.
            // Si la respuesta ya empezó a enviarse, no se puede reescribir.
            if (context.Response.HasStarted || !EsSolicitudJson(context.Request))
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = 500,
                mensaje = "Ocurrió un error interno. Intente nuevamente más tarde."
            });
        }
    }

    private static bool EsSolicitudJson(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        || request.Headers.XRequestedWith == "XMLHttpRequest"
        || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        || request.HasJsonContentType();
}