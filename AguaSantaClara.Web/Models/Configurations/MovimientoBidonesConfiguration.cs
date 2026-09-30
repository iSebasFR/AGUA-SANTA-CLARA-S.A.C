using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class MovimientoBidonesConfiguration : IEntityTypeConfiguration<MovimientoBidones>
{
    public void Configure(EntityTypeBuilder<MovimientoBidones> builder)
    {
        builder.ToTable("movimiento_bidones");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id_movimiento");
        builder.Property(m => m.IdCliente).HasColumnName("id_cliente");
        builder.Property(m => m.BidonesEntregados).HasColumnName("bidones_entregados");
        builder.Property(m => m.BidonesDevueltos).HasColumnName("bidones_devueltos");
        builder.Property(m => m.Saldo).HasColumnName("saldo");
        builder.Property(m => m.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(m => m.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(m => m.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_movimiento_bidones_entregados", "bidones_entregados >= 0");
            t.HasCheckConstraint("chk_movimiento_bidones_devueltos", "bidones_devueltos >= 0");
        });

        builder.HasOne(m => m.Cliente)
               .WithMany()
               .HasForeignKey(m => m.IdCliente)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.IdCliente).HasDatabaseName("idx_movimiento_bidones_cliente");
    }
}