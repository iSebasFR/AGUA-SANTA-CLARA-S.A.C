using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class RepartidorConfiguration : IEntityTypeConfiguration<Repartidor>
{
    public void Configure(EntityTypeBuilder<Repartidor> builder)
    {
        builder.ToTable("repartidor");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id_repartidor");
        builder.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        builder.Property(r => r.Celular).HasColumnName("celular").HasMaxLength(11).IsRequired();
        builder.Property(r => r.Estado).HasColumnName("estado");
        builder.Property(r => r.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(r => r.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(r => r.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t => t.HasCheckConstraint("chk_repartidor_celular", "celular ~ '^519[0-9]{8}$'"));

        builder.HasIndex(r => r.Celular).IsUnique().HasDatabaseName("uq_repartidor_celular");
    }
}
