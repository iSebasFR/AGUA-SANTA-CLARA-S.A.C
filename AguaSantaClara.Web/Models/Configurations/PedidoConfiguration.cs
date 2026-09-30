using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedido");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id_pedido");
        builder.Property(p => p.IdLocal).HasColumnName("id_local");
        builder.Property(p => p.IdRepartidor).HasColumnName("id_repartidor");
        builder.Property(p => p.FechaEntrega).HasColumnName("fecha_entrega").HasColumnType("timestamp without time zone");
        builder.Property(p => p.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        builder.Property(p => p.Total).HasColumnName("total").HasPrecision(12, 2);
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "chk_pedido_estado",
                "estado IN ('Pendiente', 'Enviado', 'Entregado', 'Con Incidencia', 'Pagado', 'Pago Parcial')");
            t.HasCheckConstraint("chk_pedido_total_no_negativo", "total >= 0");
        });

        builder.HasOne(p => p.Local)
               .WithMany()
               .HasForeignKey(p => p.IdLocal)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Repartidor)
               .WithMany()
               .HasForeignKey(p => p.IdRepartidor)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Estado).HasDatabaseName("idx_pedido_estado");
        builder.HasIndex(p => p.FechaEntrega).HasDatabaseName("idx_pedido_fecha_entrega");
    }
}
