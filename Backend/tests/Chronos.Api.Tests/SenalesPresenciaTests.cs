using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chronos.Api.Tests;

/// <summary>
/// Comprueba contra PostgreSQL real que el modelo de señales aguanta lo que promete:
/// varias evidencias por checada y detalles heterogéneos sin cambiar el esquema.
/// </summary>
[Collection(nameof(ColeccionApi))]
public class SenalesPresenciaTests(FabricaApiPruebas fabrica)
{
    [Fact]
    public void LasPruebasApuntanAlContenedorEfimeroYNoALaBaseDeDesarrollo()
    {
        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

        Assert.Equal("chronos_pruebas", contexto.Database.GetDbConnection().Database);
    }

    private async Task<Guid> PrimerEmpleadoAsync()
    {
        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

        return await contexto.Empleados.Select(e => e.Id).FirstAsync();
    }

    [Fact]
    public async Task UnaChecadaConservaSusSenalesYSuPuntajeAlReleerla()
    {
        var empleadoId = await PrimerEmpleadoAsync();
        var checadaId = Guid.CreateVersion7();

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var checada = new Checada
            {
                Id = checadaId,
                EmpleadoId = empleadoId,
                Tipo = TipoChecada.Entrada,
                MomentoUtc = DateTimeOffset.UtcNow,
                DiaLaboral = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada, """{"folio":"QR-8891"}""");
            checada.AgregarSenal(TipoSenal.WebAuthn, ResultadoSenal.Confirmada, """{"aaguid":"08987058"}""");

            contexto.Checadas.Add(checada);
            await contexto.SaveChangesAsync();
        }

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var recuperada = await contexto.Checadas
                .AsNoTracking()
                .Include(c => c.Senales)
                .FirstAsync(c => c.Id == checadaId);

            Assert.Equal(2, recuperada.Senales.Count);
            Assert.Equal(70, recuperada.PuntajeConfianza);
            Assert.Equal(NivelConfianza.Alta, recuperada.NivelConfianza);
            Assert.Equal(EstadoChecada.Verificada, recuperada.Estado);
        }
    }

    [Fact]
    public async Task ElDetalleDeUnaSenalSeGuardaComoJsonbConsultable()
    {
        var empleadoId = await PrimerEmpleadoAsync();
        var folio = $"QR-{Guid.CreateVersion7():N}"[..12];

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var checada = new Checada
            {
                EmpleadoId = empleadoId,
                Tipo = TipoChecada.Entrada,
                MomentoUtc = DateTimeOffset.UtcNow,
                DiaLaboral = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada, $$"""{"folio":"{{folio}}"}""");

            contexto.Checadas.Add(checada);
            await contexto.SaveChangesAsync();
        }

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            // El operador ->> solo funciona si la columna es jsonb de verdad, no texto.
            var encontradas = await contexto.Database
                .SqlQueryRaw<int>(
                    """SELECT COUNT(*)::int AS "Value" FROM senales_presencia WHERE detalle_json->>'folio' = {0}""",
                    folio)
                .SingleAsync();

            Assert.Equal(1, encontradas);
        }
    }

    [Fact]
    public async Task SumarUnTipoDeSenalNuevoNoRequiereCambiarElEsquema()
    {
        var empleadoId = await PrimerEmpleadoAsync();
        var checadaId = Guid.CreateVersion7();

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var checada = new Checada
            {
                Id = checadaId,
                EmpleadoId = empleadoId,
                Tipo = TipoChecada.Entrada,
                MomentoUtc = DateTimeOffset.UtcNow,
                DiaLaboral = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            // Carga propia de un iBeacon, que es justo lo que traerá la app de la fase 2.
            checada.AgregarSenal(
                TipoSenal.BeaconBle,
                ResultadoSenal.Confirmada,
                """{"uuid":"f7826da6-4fa2-4e98-8024-bc5b71e0893e","major":1,"minor":42,"rssi":-67}""");

            checada.AgregarSenal(TipoSenal.WebAuthn, ResultadoSenal.Confirmada);

            contexto.Checadas.Add(checada);
            await contexto.SaveChangesAsync();
        }

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var beacon = await contexto.SenalesPresencia
                .AsNoTracking()
                .FirstAsync(s => s.ChecadaId == checadaId && s.Tipo == TipoSenal.BeaconBle);

            Assert.Equal(20, beacon.PesoAplicado);
            Assert.Contains("f7826da6", beacon.DetalleJson);
        }
    }

    [Fact]
    public async Task UnaChecadaSinEvidenciaQuedaRechazadaEnLaBase()
    {
        var empleadoId = await PrimerEmpleadoAsync();
        var checadaId = Guid.CreateVersion7();

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var checada = new Checada
            {
                Id = checadaId,
                EmpleadoId = empleadoId,
                Tipo = TipoChecada.Entrada,
                MomentoUtc = DateTimeOffset.UtcNow,
                DiaLaboral = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Fallida);

            contexto.Checadas.Add(checada);
            await contexto.SaveChangesAsync();
        }

        using (var alcance = fabrica.Services.CreateScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ChronosDbContext>();

            var recuperada = await contexto.Checadas.AsNoTracking().FirstAsync(c => c.Id == checadaId);

            Assert.Equal(EstadoChecada.Rechazada, recuperada.Estado);
            Assert.Equal(0, recuperada.PuntajeConfianza);
        }
    }
}
