using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class SenalPresenciaConfiguracion : IEntityTypeConfiguration<SenalPresencia>
{
    public void Configure(EntityTypeBuilder<SenalPresencia> builder)
    {
        // jsonb permite consultar el detalle de cada tipo de señal sin columnas dedicadas:
        // al sumar beacons basta con escribir otra forma de JSON, no otra migración.
        builder.Property(s => s.DetalleJson).HasColumnType("jsonb");

        builder.HasIndex(s => new { s.ChecadaId, s.Tipo });
        builder.HasIndex(s => s.Tipo);
    }
}
