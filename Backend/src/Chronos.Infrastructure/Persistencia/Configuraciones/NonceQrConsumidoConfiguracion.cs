using Chronos.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Infrastructure.Persistencia.Configuraciones;

public class NonceQrConsumidoConfiguracion : IEntityTypeConfiguration<NonceQrConsumido>
{
    public void Configure(EntityTypeBuilder<NonceQrConsumido> builder)
    {
        builder.ToTable("nonces_qr_consumidos");

        builder.HasKey(n => n.Nonce);

        // No se declara llave foránea a checadas ni a empleados: el asiento debe sobrevivir
        // aunque la checada se borre, porque su única razón de existir es impedir que el
        // mismo código se vuelva a usar.
        builder.Property(n => n.Nonce).ValueGeneratedNever();

        // La purga de asientos vencidos barre por esta columna.
        builder.HasIndex(n => n.ExpiraUtc);
    }
}
