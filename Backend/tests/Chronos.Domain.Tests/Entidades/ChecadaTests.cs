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
}
