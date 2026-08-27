using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class ChecadaConfiguracion : IEntityTypeConfiguration<Checada>
{
    public void Configure(EntityTypeBuilder<Checada> builder)
    {
        builder.Property(c => c.HuellaDispositivo).HasMaxLength(128);
        builder.Property(c => c.DireccionIp).HasMaxLength(45);
        builder.Property(c => c.Observaciones).HasMaxLength(500);
        builder.Property(c => c.MotivoAjuste).HasMaxLength(500);

        builder.Ignore(c => c.CuentaParaJornada);

        builder.HasIndex(c => new { c.EmpleadoId, c.DiaLaboral });
        builder.HasIndex(c => c.MomentoUtc);

        builder.HasIndex(c => c.Estado)
            .HasFilter($"estado = {(int)EstadoChecada.RequiereRevision}");

        builder.HasOne(c => c.Empleado)
            .WithMany(e => e.Checadas)
            .HasForeignKey(c => c.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Sede)
            .WithMany()
            .HasForeignKey(c => c.SedeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Senales)
            .WithOne(s => s.Checada)
            .HasForeignKey(s => s.ChecadaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
