using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Tests.Entidades;

public class ChecadaTests
{
    private static Checada Nueva() => new()
    {
        EmpleadoId = Guid.CreateVersion7(),
        Tipo = TipoChecada.Entrada,
        MomentoUtc = DateTimeOffset.UtcNow,
        DiaLaboral = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    [Fact]
    public void UnaChecadaRecienCreadaNoCuentaHastaTenerEvidencia()
    {
        var checada = Nueva();

        Assert.Equal(EstadoChecada.Rechazada, checada.Estado);
        Assert.False(checada.CuentaParaJornada);
    }

    [Fact]
    public void AgregarSenalesRecalculaElEstadoAlVuelo()
    {
        var checada = Nueva();

        checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada);
        Assert.Equal(EstadoChecada.RequiereRevision, checada.Estado);

        checada.AgregarSenal(TipoSenal.WebAuthn, ResultadoSenal.Confirmada);
        Assert.Equal(EstadoChecada.Verificada, checada.Estado);
        Assert.Equal(NivelConfianza.Alta, checada.NivelConfianza);
    }

    [Fact]
    public void ElDetalleDeCadaSenalSeConservaTalCual()
    {
        var checada = Nueva();
        const string detalle = """{"uuid":"f7826da6","major":1,"minor":42,"rssi":-67}""";

        var senal = checada.AgregarSenal(TipoSenal.BeaconBle, ResultadoSenal.Confirmada, detalle);

        Assert.Equal(detalle, senal.DetalleJson);
        Assert.Equal(TipoSenal.BeaconBle, senal.Tipo);
    }

    [Fact]
    public void ElAjusteDeUnSupervisorSobreviveARecalculos()
    {
        var checada = Nueva();
        checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada);

        var supervisor = Guid.CreateVersion7();
        checada.AjustarPorSupervisor(supervisor, "Falla del lector biométrico, validado en sitio.");

        checada.Reevaluar();

        Assert.Equal(EstadoChecada.AjustadaPorSupervisor, checada.Estado);
        Assert.Equal(supervisor, checada.AjustadaPorUsuarioId);
        Assert.True(checada.CuentaParaJornada);
    }

    [Fact]
    public void UnAjusteExigeMotivo()
    {
        var checada = Nueva();

        Assert.Throws<ArgumentException>(() => checada.AjustarPorSupervisor(Guid.CreateVersion7(), "   "));
    }

    [Fact]
    public void UnRechazoDeSupervisorSacaLaChecadaDeLaJornadaSinBorrarla()
    {
        var checada = Nueva();
        checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada);

        var supervisor = Guid.CreateVersion7();
        checada.RechazarPorSupervisor(supervisor, "El empleado estaba de vacaciones ese día.");

        Assert.Equal(EstadoChecada.Rechazada, checada.Estado);
        Assert.False(checada.CuentaParaJornada);
        Assert.Equal(supervisor, checada.AjustadaPorUsuarioId);
        Assert.Equal("El empleado estaba de vacaciones ese día.", checada.MotivoAjuste);
    }

    [Fact]
    public void UnRechazoExigeMotivo()
    {
        var checada = Nueva();

        Assert.Throws<ArgumentException>(() => checada.RechazarPorSupervisor(Guid.CreateVersion7(), ""));
    }

    [Fact]
    public void UnaSenalTardiaNoResucitaUnaChecadaRechazadaEnRevision()
    {
        // El caso perverso: rechazada por un humano, el estado coincide con el que produce
        // una evaluación automática débil. Si Reevaluar mirara el estado en vez de quién
        // decidió, una señal que llegue tarde borraría el dictamen sin dejar rastro.
        var checada = Nueva();
        checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada);
        checada.RechazarPorSupervisor(Guid.CreateVersion7(), "Sin evidencia de presencia en sede.");

        checada.AgregarSenal(TipoSenal.WebAuthn, ResultadoSenal.Confirmada);

        Assert.Equal(EstadoChecada.Rechazada, checada.Estado);
        Assert.False(checada.CuentaParaJornada);
    }

    [Fact]
    public void SoloEsperaRevisionMientrasNadieLaDictamine()
    {
        var checada = Nueva();
        checada.AgregarSenal(TipoSenal.CodigoQr, ResultadoSenal.Confirmada);

        Assert.True(checada.EsperaRevision);

        checada.AjustarPorSupervisor(Guid.CreateVersion7(), "Confirmado con el jefe de sede.");

        Assert.False(checada.EsperaRevision);
    }
}
