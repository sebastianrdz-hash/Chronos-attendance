using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class EmpleadoConfiguracion : IEntityTypeConfiguration<Empleado>
{
    public void Configure(EntityTypeBuilder<Empleado> builder)
    {
        builder.Property(e => e.NumeroEmpleado).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Nombres).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ApellidoPaterno).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ApellidoMaterno).HasMaxLength(120);
        builder.Property(e => e.CorreoCorporativo).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Puesto).HasMaxLength(160);

        builder.HasIndex(e => e.NumeroEmpleado).IsUnique();
        builder.HasIndex(e => e.CorreoCorporativo).IsUnique();
        builder.HasIndex(e => e.UsuarioId).IsUnique().HasFilter("usuario_id IS NOT NULL");

        builder.Ignore(e => e.NombreCompleto);

        builder.HasOne(e => e.Departamento)
            .WithMany(d => d.Empleados)
            .HasForeignKey(e => e.DepartamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Sede)
            .WithMany(s => s.Empleados)
            .HasForeignKey(e => e.SedeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Turno)
            .WithMany(t => t.Empleados)
            .HasForeignKey(e => e.TurnoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
