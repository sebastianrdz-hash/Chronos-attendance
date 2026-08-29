using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class AsientoBitacoraConfiguracion : IEntityTypeConfiguration<AsientoBitacora>
{
    public void Configure(EntityTypeBuilder<AsientoBitacora> builder)
    {
        builder.ToTable("bitacora");

        builder.Property(a => a.Entidad).HasMaxLength(60).IsRequired();
        builder.Property(a => a.UsuarioCorreo).HasMaxLength(256);
        builder.Property(a => a.Motivo).HasMaxLength(500);
        builder.Property(a => a.DireccionIp).HasMaxLength(45);
        builder.Property(a => a.DatosJson).HasColumnType("jsonb");

        // Las dos consultas que se hacen de verdad: el historial de un expediente concreto
        // y el recorrido cronológico de todo lo ocurrido.
        builder.HasIndex(a => new { a.Entidad, a.EntidadId });
        builder.HasIndex(a => a.OcurridoUtc).IsDescending();

        // Sin relación de navegación hacia el usuario: un asiento no debe desaparecer en
        // cascada si la cuenta que lo provocó se elimina.
    }
}
