using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class InsumoConfiguration : IEntityTypeConfiguration<Insumo>
{
    public void Configure(EntityTypeBuilder<Insumo> builder)
    {
        builder.ToTable("insumo");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id_insumo");
        builder.Property(i => i.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        builder.Property(i => i.Descripcion).HasColumnName("descripcion").HasMaxLength(255);
        builder.Property(i => i.Costo).HasColumnName("costo").HasPrecision(12, 2);
        builder.Property(i => i.Estado).HasColumnName("estado");
        builder.Property(i => i.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(i => i.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(i => i.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_insumo_costo_positivo", "costo > 0");
        });

        builder.HasIndex(i => i.Nombre).HasDatabaseName("idx_insumo_nombre");
    }
}