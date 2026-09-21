namespace AguaSantaClara.Web.Services;

public interface IGenericService<T> where T : class
{
    Task<IEnumerable<T>> ListarAsync();
    Task<T?> ObtenerPorIdAsync(long id);
    Task CrearAsync(T entity);
    Task ActualizarAsync(T entity);
    Task EliminarAsync(long id);
}