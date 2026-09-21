using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class DireccionClienteConfiguration : IEntityTypeConfiguration<DireccionCliente>
{
    public void Configure(EntityTypeBuilder<DireccionCliente> builder)
    {
        builder.ToTable("direccion_cliente");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id_direccion");
        builder.Property(d => d.IdCliente).HasColumnName("id_cliente");
        builder.Property(d => d.Direccion).HasColumnName("direccion").HasMaxLength(255).IsRequired();
        builder.Property(d => d.Referencia).HasColumnName("referencia").HasMaxLength(255);
        builder.Property(d => d.Ciudad).HasColumnName("ciudad").HasMaxLength(100);
        builder.Property(d => d.Principal).HasColumnName("principal");
        builder.Property(d => d.Estado).HasColumnName("estado");
        builder.Property(d => d.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(d => d.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(d => d.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasOne(d => d.Cliente)
               .WithMany(c => c.Direcciones)
               .HasForeignKey(d => d.IdCliente)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.IdCliente).HasDatabaseName("idx_direccion_cliente");
    }
}