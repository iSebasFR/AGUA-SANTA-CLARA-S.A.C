using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class GastoRutaConfiguration : IEntityTypeConfiguration<GastoRuta>
{
    public void Configure(EntityTypeBuilder<GastoRuta> builder)
    {
        builder.ToTable("gasto_ruta");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id_gasto");
        builder.Property(g => g.IdRepartidor).HasColumnName("id_repartidor");
        builder.Property(g => g.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        builder.Property(g => g.Monto).HasColumnName("monto").HasPrecision(12, 2);
        builder.Property(g => g.Fecha).HasColumnName("fecha").HasColumnType("date");
        builder.Property(g => g.Observacion).HasColumnName("observacion").HasMaxLength(255);
        builder.Property(g => g.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(g => g.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(g => g.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_gasto_ruta_monto_positivo", "monto > 0");
            t.HasCheckConstraint(
                "chk_gasto_ruta_tipo",
                "tipo IN ('Gasolina', 'Peajes', 'Cochera', 'Otros')");
        });

        builder.HasOne(g => g.Repartidor)
               .WithMany()
               .HasForeignKey(g => g.IdRepartidor)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => g.IdRepartidor).HasDatabaseName("idx_gasto_ruta_repartidor");
        builder.HasIndex(g => g.Fecha).HasDatabaseName("idx_gasto_ruta_fecha");
    }
}