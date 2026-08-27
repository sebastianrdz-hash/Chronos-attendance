using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class TurnoConfiguracion : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.Property(t => t.Nombre).HasMaxLength(120).IsRequired();

        builder.HasIndex(t => t.Nombre).IsUnique();

        builder.Ignore(t => t.CruzaMedianoche);
        builder.Ignore(t => t.DuracionProgramada);
    }
}
