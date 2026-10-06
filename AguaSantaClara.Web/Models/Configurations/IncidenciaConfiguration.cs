using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class IncidenciaConfiguration : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.ToTable("incidencia");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id_incidencia");
        builder.Property(i => i.IdPedido).HasColumnName("id_pedido");
        builder.Property(i => i.Motivo).HasColumnName("motivo").HasMaxLength(50).IsRequired();
        builder.Property(i => i.Detalle).HasColumnName("detalle").HasMaxLength(500);
        builder.Property(i => i.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(i => i.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(i => i.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_incidencia_motivo",
            "motivo IN ('Cliente ausente', 'Dirección incorrecta', 'Producto dañado', 'Vehículo descompuesto', 'Otros')"));

        builder.HasOne(i => i.Pedido)
               .WithMany()
               .HasForeignKey(i => i.IdPedido)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.UsuarioReporta)
                .WithMany()
                .HasForeignKey(i => i.IdUsuarioReporta)
                .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.IdPedido).HasDatabaseName("idx_incidencia_pedido");
        builder.Property(i => i.IdUsuarioReporta)
               .HasColumnName("id_usuario_reporta");
    }
}