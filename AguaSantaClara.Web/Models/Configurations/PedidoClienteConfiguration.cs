using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class PedidoClienteConfiguration : IEntityTypeConfiguration<PedidoCliente>
{
    public void Configure(EntityTypeBuilder<PedidoCliente> builder)
    {
        builder.ToTable("pedido_cliente");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id_pedido_cliente");
        builder.Property(p => p.IdPedido).HasColumnName("id_pedido");
        builder.Property(p => p.IdCliente).HasColumnName("id_cliente");
        builder.Property(p => p.IdDireccion).HasColumnName("id_direccion");
        builder.Property(p => p.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t => t.HasCheckConstraint("chk_pedido_cliente_subtotal_no_negativo", "subtotal >= 0"));

        builder.HasOne(p => p.Pedido)
               .WithMany(pe => pe.Clientes)
               .HasForeignKey(p => p.IdPedido)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Cliente)
               .WithMany()
               .HasForeignKey(p => p.IdCliente)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Direccion)
               .WithMany()
               .HasForeignKey(p => p.IdDireccion)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.IdPedido, p.IdCliente, p.IdDireccion })
               .IsUnique()
               .HasDatabaseName("uq_pedido_cliente_direccion");
        builder.HasIndex(p => p.IdCliente).HasDatabaseName("idx_pedido_cliente_cliente");
    }
}
