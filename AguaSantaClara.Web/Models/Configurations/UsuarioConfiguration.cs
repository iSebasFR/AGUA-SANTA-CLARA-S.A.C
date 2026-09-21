using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");

        builder.Property(u => u.Id).HasColumnName("id_usuario");
        builder.Property(u => u.Nombres).HasColumnName("nombres").HasMaxLength(100).IsRequired();
        builder.Property(u => u.Apellidos).HasColumnName("apellidos").HasMaxLength(100).IsRequired();
        builder.Property(u => u.IdRol).HasColumnName("id_rol");
        builder.Property(u => u.Estado).HasColumnName("estado");
        builder.Property(u => u.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(u => u.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.HasOne(u => u.Rol)
               .WithMany(r => r.Usuarios)
               .HasForeignKey(u => u.IdRol)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.UserName).HasDatabaseName("idx_usuario_username");
    }
}