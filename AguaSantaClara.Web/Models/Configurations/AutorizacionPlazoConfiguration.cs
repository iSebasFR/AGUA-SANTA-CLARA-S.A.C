using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class AutorizacionPlazoConfiguration : IEntityTypeConfiguration<AutorizacionPlazo>
{
    public void Configure(EntityTypeBuilder<AutorizacionPlazo> builder)
    {
        builder.ToTable("autorizacion_plazo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id_autorizacion");
        builder.Property(a => a.IdDeuda).HasColumnName("id_deuda");
        builder.Property(a => a.IdUsuarioAutoriza).HasColumnName("id_usuario_autoriza");

        builder.Property(a => a.MontoAnterior).HasColumnName("monto_anterior").HasPrecision(12, 2);
        builder.Property(a => a.MontoNuevo).HasColumnName("monto_nuevo").HasPrecision(12, 2);
        builder.Property(a => a.FechaVencimientoAnterior).HasColumnName("fecha_vencimiento_anterior").HasColumnType("date");
        builder.Property(a => a.FechaVencimientoNueva).HasColumnName("fecha_vencimiento_nueva").HasColumnType("date");

        builder.Property(a => a.Observacion).HasColumnName("observacion").HasMaxLength(255);
        builder.Property(a => a.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(a => a.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(a => a.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_autorizacion_monto_anterior_positivo", "monto_anterior > 0");
            t.HasCheckConstraint("chk_autorizacion_monto_nuevo_positivo", "monto_nuevo > 0");
        });

        builder.HasOne(a => a.Deuda)
               .WithMany(d => d.Autorizaciones)
               .HasForeignKey(a => a.IdDeuda)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UsuarioAutoriza)
               .WithMany()
               .HasForeignKey(a => a.IdUsuarioAutoriza)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.IdDeuda).HasDatabaseName("idx_autorizacion_deuda");
        builder.HasIndex(a => a.IdUsuarioAutoriza).HasDatabaseName("idx_autorizacion_usuario");
    }
}