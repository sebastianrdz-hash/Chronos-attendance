using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class DesafioWebAuthnConfiguracion : IEntityTypeConfiguration<DesafioWebAuthn>
{
    public void Configure(EntityTypeBuilder<DesafioWebAuthn> builder)
    {
        builder.ToTable("desafios_webauthn");

        builder.Property(d => d.OpcionesJson).HasColumnType("jsonb");
        builder.Property(d => d.NombreDispositivo).HasMaxLength(120);

        // Un empleado tiene a lo sumo un desafío vivo por propósito. Pedir uno nuevo
        // reemplaza el anterior, de modo que dos pestañas abiertas no dejen retos sueltos
        // que sigan siendo canjeables.
        builder.HasIndex(d => new { d.EmpleadoId, d.Proposito }).IsUnique();

        builder.HasIndex(d => d.ExpiraUtc);

        builder.HasOne<Empleado>()
            .WithMany()
            .HasForeignKey(d => d.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
