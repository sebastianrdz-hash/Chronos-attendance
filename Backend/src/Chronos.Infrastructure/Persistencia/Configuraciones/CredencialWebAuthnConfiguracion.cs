using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class CredencialWebAuthnConfiguracion : IEntityTypeConfiguration<CredencialWebAuthn>
{
    public void Configure(EntityTypeBuilder<CredencialWebAuthn> builder)
    {
        builder.Property(c => c.NombreAmigable).HasMaxLength(120);
        builder.Property(c => c.TipoDispositivo).HasMaxLength(60);

        builder.HasIndex(c => c.CredentialId).IsUnique();
        builder.HasIndex(c => c.EmpleadoId);

        builder.HasOne(c => c.Empleado)
            .WithMany(e => e.Credenciales)
            .HasForeignKey(c => c.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
