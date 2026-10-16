using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class CierreCajaConfiguration : IEntityTypeConfiguration<CierreCaja>
{
    public void Configure(EntityTypeBuilder<CierreCaja> builder)
    {
        builder.ToTable("cierre_caja");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id_cierre");
        builder.Property(c => c.IdVendedora).HasColumnName("id_vendedora");
        builder.Property(c => c.Fecha).HasColumnName("fecha").HasColumnType("date");

        builder.Property(c => c.EfectivoRecibido).HasColumnName("efectivo_recibido").HasPrecision(12, 2);
        builder.Property(c => c.YapeRecibido).HasColumnName("yape_recibido").HasPrecision(12, 2);
        builder.Property(c => c.GastosDelDia).HasColumnName("gastos_del_dia").HasPrecision(12, 2);
        builder.Property(c => c.EfectivoEsperado).HasColumnName("efectivo_esperado").HasPrecision(12, 2);
        builder.Property(c => c.YapeEsperado).HasColumnName("yape_esperado").HasPrecision(12, 2);
        builder.Property(c => c.Diferencia).HasColumnName("diferencia").HasPrecision(12, 2);

        builder.Property(c => c.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        builder.Property(c => c.Observacion).HasColumnName("observacion").HasMaxLength(255);
        builder.Property(c => c.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(c => c.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(c => c.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_cierre_efectivo_no_negativo", "efectivo_recibido >= 0");
            t.HasCheckConstraint("chk_cierre_yape_no_negativo", "yape_recibido >= 0");
            t.HasCheckConstraint("chk_cierre_gastos_no_negativo", "gastos_del_dia >= 0");
            t.HasCheckConstraint(
                "chk_cierre_estado",
                "estado IN ('Cuadrado', 'Con diferencia')");
        });

        builder.HasOne(c => c.Vendedora)
               .WithMany()
               .HasForeignKey(c => c.IdVendedora)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.IdVendedora).HasDatabaseName("idx_cierre_caja_vendedora");
        builder.HasIndex(c => c.Fecha).HasDatabaseName("idx_cierre_caja_fecha");
        builder.HasIndex(c => new { c.IdVendedora, c.Fecha })
               .IsUnique()
               .HasFilter("estado_registro = true")
               .HasDatabaseName("uq_cierre_caja_vendedora_fecha");
    }
}