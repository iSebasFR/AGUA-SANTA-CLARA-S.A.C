using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("cliente");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id_cliente");
        builder.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        builder.Property(c => c.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(150);
        builder.Property(c => c.Estado).HasColumnName("estado");
        builder.Property(c => c.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(c => c.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(c => c.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasIndex(c => c.Nombre).HasDatabaseName("idx_cliente_nombre");
        builder.HasIndex(c => c.Telefono).HasDatabaseName("idx_cliente_telefono");
    }
}