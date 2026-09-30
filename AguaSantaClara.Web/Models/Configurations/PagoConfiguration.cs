using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class PagoConfiguration : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("pago");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id_pago");
        builder.Property(p => p.IdPedido).HasColumnName("id_pedido");
        builder.Property(p => p.IdCliente).HasColumnName("id_cliente");
        builder.Property(p => p.IdMetodoPago).HasColumnName("id_metodo_pago");
        builder.Property(p => p.Monto).HasColumnName("monto").HasPrecision(12, 2);
        builder.Property(p => p.FechaPago).HasColumnName("fecha_pago");
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t => t.HasCheckConstraint("chk_pago_monto_positivo", "monto > 0"));

        builder.HasOne(p => p.Pedido)
               .WithMany()
               .HasForeignKey(p => p.IdPedido)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Cliente)
               .WithMany()
               .HasForeignKey(p => p.IdCliente)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.MetodoPago)
               .WithMany(m => m.Pagos)
               .HasForeignKey(p => p.IdMetodoPago)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.IdPedido).HasDatabaseName("idx_pago_pedido");
        builder.HasIndex(p => p.IdCliente).HasDatabaseName("idx_pago_cliente");
    }
}