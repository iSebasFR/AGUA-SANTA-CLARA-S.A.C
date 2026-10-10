using AguaSantaClara.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AguaSantaClara.Web.Models.Configurations;

public class DeudaConfiguration : IEntityTypeConfiguration<Deuda>
{
    public void Configure(EntityTypeBuilder<Deuda> builder)
    {
        builder.ToTable("deuda");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id_deuda");
        builder.Property(d => d.IdPedido).HasColumnName("id_pedido");
        builder.Property(d => d.IdCliente).HasColumnName("id_cliente");
        builder.Property(d => d.Monto).HasColumnName("monto").HasPrecision(12, 2);
        builder.Property(d => d.MontoPagadoAlRegistrar).HasColumnName("monto_pagado_al_registrar").HasPrecision(12, 2);
        builder.Property(d => d.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
        builder.Property(d => d.RegistradaFormalmente).HasColumnName("registrada_formalmente");
        builder.Property(d => d.Estado).HasColumnName("estado").HasMaxLength(20);
        builder.Property(d => d.EstadoRegistro).HasColumnName("estado_registro");
        builder.Property(d => d.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(d => d.FechaActualizacion).HasColumnName("fecha_actualizacion");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_deuda_monto_positivo", "monto > 0");
            t.HasCheckConstraint("chk_deuda_monto_pagado_no_negativo", "monto_pagado_al_registrar >= 0");
            t.HasCheckConstraint("chk_deuda_estado", "estado IN ('Pendiente', 'Pagada', 'Vencida')");
        });

        builder.HasOne(d => d.Pedido)
               .WithMany()
               .HasForeignKey(d => d.IdPedido)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Cliente)
               .WithMany()
               .HasForeignKey(d => d.IdCliente)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.IdCliente).HasDatabaseName("idx_deuda_cliente");
        builder.HasIndex(d => d.Estado).HasDatabaseName("idx_deuda_estado");
        builder.HasIndex(d => d.FechaVencimiento).HasDatabaseName("idx_deuda_vencimiento");
    }
}