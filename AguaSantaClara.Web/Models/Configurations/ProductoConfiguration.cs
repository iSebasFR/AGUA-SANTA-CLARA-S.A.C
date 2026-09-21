using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("producto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id_producto");
        builder.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        builder.Property(p => p.Descripcion).HasColumnName("descripcion").HasMaxLength(255);
        builder.Property(p => p.PrecioVenta).HasColumnName("precio_venta").HasPrecision(12, 2);
        builder.Property(p => p.Costo).HasColumnName("costo").HasPrecision(12, 2);
        builder.Property(p => p.StockActual).HasColumnName("stock_actual");
        builder.Property(p => p.StockMinimo).HasColumnName("stock_minimo");
        builder.Property(p => p.Estado).HasColumnName("estado");
        builder.Property(p => p.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(p => p.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_producto_precio_positivo", "precio_venta > 0");
            t.HasCheckConstraint("chk_producto_costo_positivo", "costo > 0");
            t.HasCheckConstraint("chk_producto_stock_no_negativo", "stock_actual >= 0 AND stock_minimo >= 0");
        });

        builder.HasIndex(p => p.Nombre).HasDatabaseName("idx_producto_nombre");
    }
}