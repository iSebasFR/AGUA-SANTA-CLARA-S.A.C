using AguaSantaClara.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AguaSantaClara.Web.Data;

public class AppDbContext : IdentityDbContext<Usuario, Rol, long>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<DireccionCliente> DireccionesCliente => Set<DireccionCliente>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            var propCreacion = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "FechaCreacion");
            var propActualizacion = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "FechaActualizacion");

            if (entry.State == EntityState.Added && propCreacion != null)
                propCreacion.CurrentValue = DateTime.UtcNow;

            if (entry.State == EntityState.Modified && propActualizacion != null)
                propActualizacion.CurrentValue = DateTime.UtcNow;
        }
        return base.SaveChanges();
    }
}