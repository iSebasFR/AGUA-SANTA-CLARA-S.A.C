using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class LocalConfiguration : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("local");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id_local");
        builder.Property(l => l.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Estado).HasColumnName("estado");
        builder.Property(l => l.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(l => l.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(l => l.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasIndex(l => l.Nombre).IsUnique().HasDatabaseName("uq_local_nombre");
    }
}
