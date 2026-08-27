using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class DepartamentoConfiguracion : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> builder)
    {
        builder.Property(d => d.Nombre).HasMaxLength(160).IsRequired();
        builder.Property(d => d.Codigo).HasMaxLength(20).IsRequired();

        builder.HasIndex(d => new { d.SedeId, d.Codigo }).IsUnique();

        builder.HasOne(d => d.Sede)
            .WithMany(s => s.Departamentos)
            .HasForeignKey(d => d.SedeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
