using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class AuditoriaCambioConfiguration : IEntityTypeConfiguration<AuditoriaCambio>
{
    public void Configure(EntityTypeBuilder<AuditoriaCambio> builder)
    {
        builder.ToTable("auditoria_cambio");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id_auditoria");
        builder.Property(a => a.IdUsuario).HasColumnName("id_usuario");
        builder.Property(a => a.NombreUsuario).HasColumnName("nombre_usuario").HasMaxLength(150).IsRequired();
        builder.Property(a => a.Entidad).HasColumnName("entidad").HasMaxLength(100).IsRequired();
        builder.Property(a => a.IdRegistro).HasColumnName("id_registro").HasMaxLength(100);
        builder.Property(a => a.Accion).HasColumnName("accion").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Fecha).HasColumnName("fecha");
        builder.Property(a => a.ValoresAnteriores).HasColumnName("valores_anteriores").HasColumnType("jsonb");
        builder.Property(a => a.ValoresNuevos).HasColumnName("valores_nuevos").HasColumnType("jsonb");

        builder.HasIndex(a => a.Fecha).HasDatabaseName("idx_auditoria_fecha");
        builder.HasIndex(a => new { a.Entidad, a.IdRegistro }).HasDatabaseName("idx_auditoria_entidad_registro");
    }
}
