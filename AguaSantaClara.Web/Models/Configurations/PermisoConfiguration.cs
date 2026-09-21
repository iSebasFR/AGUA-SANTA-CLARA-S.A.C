using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("permiso");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id_permiso");
        builder.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Descripcion).HasColumnName("descripcion").HasMaxLength(200);
        builder.Property(p => p.Estado).HasColumnName("estado");
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("uq_permiso_codigo");
    }
}