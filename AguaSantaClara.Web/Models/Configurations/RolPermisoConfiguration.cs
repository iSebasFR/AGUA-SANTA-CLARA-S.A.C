using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("rol_permiso");
        builder.HasKey(rp => new { rp.IdRol, rp.IdPermiso });

        builder.Property(rp => rp.IdRol).HasColumnName("id_rol");
        builder.Property(rp => rp.IdPermiso).HasColumnName("id_permiso");
        builder.Property(rp => rp.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(rp => rp.FechaCreacion).HasColumnName("fecha_creacion");

        builder.HasOne(rp => rp.Rol)
               .WithMany(r => r.RolPermisos)
               .HasForeignKey(rp => rp.IdRol)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permiso)
               .WithMany(p => p.RolPermisos)
               .HasForeignKey(rp => rp.IdPermiso)
               .OnDelete(DeleteBehavior.Cascade);
    }
}