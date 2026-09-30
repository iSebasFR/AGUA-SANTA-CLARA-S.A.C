using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class DetallePedidoConfiguration : IEntityTypeConfiguration<DetallePedido>
{
    public void Configure(EntityTypeBuilder<DetallePedido> builder)
    {
        builder.ToTable("detalle_pedido");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id_detalle_pedido");
        builder.Property(d => d.IdPedidoCliente).HasColumnName("id_pedido_cliente");
        builder.Property(d => d.IdProducto).HasColumnName("id_producto");
        builder.Property(d => d.Cantidad).HasColumnName("cantidad");
        builder.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
        builder.Property(d => d.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(d => d.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(d => d.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(d => d.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_detalle_pedido_cantidad_positiva", "cantidad > 0");
            t.HasCheckConstraint("chk_detalle_pedido_precio_positivo", "precio_unitario > 0");
        });

        builder.HasOne(d => d.PedidoCliente)
               .WithMany(p => p.Detalles)
               .HasForeignKey(d => d.IdPedidoCliente)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Producto)
               .WithMany()
               .HasForeignKey(d => d.IdProducto)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.IdPedidoCliente).HasDatabaseName("idx_detalle_pedido_pedido_cliente");
        builder.HasIndex(d => d.IdProducto).HasDatabaseName("idx_detalle_pedido_producto");
    }
}
