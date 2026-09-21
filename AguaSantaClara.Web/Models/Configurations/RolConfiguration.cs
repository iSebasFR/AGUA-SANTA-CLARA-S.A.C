using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("rol");

        builder.Property(r => r.Id).HasColumnName("id_rol");
        builder.Property(r => r.Codigo).HasColumnName("codigo").HasMaxLength(30).IsRequired();
        builder.Property(r => r.Descripcion).HasColumnName("descripcion").HasMaxLength(200);
        builder.Property(r => r.Estado).HasColumnName("estado");
        builder.Property(r => r.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(r => r.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(r => r.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasIndex(r => r.Codigo).IsUnique().HasDatabaseName("uq_rol_codigo");
    }
}