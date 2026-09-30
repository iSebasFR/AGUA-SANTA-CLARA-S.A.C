using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class ProductoLocalConfiguration : IEntityTypeConfiguration<ProductoLocal>
{
    public void Configure(EntityTypeBuilder<ProductoLocal> builder)
    {
        builder.ToTable("producto_local");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id_producto_local");
        builder.Property(p => p.IdLocal).HasColumnName("id_local");
        builder.Property(p => p.IdProducto).HasColumnName("id_producto");
        builder.Property(p => p.Stock).HasColumnName("stock");
        builder.Property(p => p.Estado).HasColumnName("estado");
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t => t.HasCheckConstraint("chk_producto_local_stock_no_negativo", "stock >= 0"));

        builder.HasOne(p => p.Local)
               .WithMany(l => l.Productos)
               .HasForeignKey(p => p.IdLocal)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Producto)
               .WithMany()
               .HasForeignKey(p => p.IdProducto)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.IdLocal, p.IdProducto }).IsUnique().HasDatabaseName("uq_producto_local");
    }
}
