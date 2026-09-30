using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class MetodoPagoConfiguration : IEntityTypeConfiguration<MetodoPago>
{
    public void Configure(EntityTypeBuilder<MetodoPago> builder)
    {
        builder.ToTable("metodo_pago");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id_metodo_pago");
        builder.Property(m => m.Nombre).HasColumnName("nombre").HasMaxLength(50).IsRequired();
        builder.Property(m => m.Estado).HasColumnName("estado");
        builder.Property(m => m.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(m => m.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(m => m.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasIndex(m => m.Nombre).IsUnique().HasDatabaseName("uq_metodo_pago_nombre");
    }
}