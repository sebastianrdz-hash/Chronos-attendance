using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class SedeConfiguracion : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> builder)
    {
        builder.Property(s => s.Nombre).HasMaxLength(160).IsRequired();
        builder.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Direccion).HasMaxLength(400);
        builder.Property(s => s.ZonaHoraria).HasMaxLength(64).IsRequired();

        builder.HasIndex(s => s.Codigo).IsUnique();

        builder.OwnsOne(s => s.Geocerca, geocerca =>
        {
            geocerca.Property(g => g.Latitud).HasColumnName("geocerca_latitud");
            geocerca.Property(g => g.Longitud).HasColumnName("geocerca_longitud");
            geocerca.Property(g => g.RadioMetros).HasColumnName("geocerca_radio_metros");
        });
    }
}
