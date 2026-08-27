using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;

namespace Chronos.Domain.Tests.Reglas;

public class EvaluadorConfianzaTests
{
    private static SenalPresencia Senal(TipoSenal tipo, ResultadoSenal resultado = ResultadoSenal.Confirmada) =>
        new() { Tipo = tipo, Resultado = resultado };

    [Fact]
    public void SinSenalesLaChecadaQuedaRechazada()
    {
        var evaluacion = EvaluadorConfianza.Evaluar([]);

        Assert.Equal(0, evaluacion.Puntaje);
        Assert.Equal(NivelConfianza.Nula, evaluacion.Nivel);
        Assert.Equal(EstadoChecada.Rechazada, evaluacion.Estado);
    }

    [Fact]
    public void QrMasWebAuthnDaConfianzaAlta()
    {
        var evaluacion = EvaluadorConfianza.Evaluar(
            [Senal(TipoSenal.CodigoQr), Senal(TipoSenal.WebAuthn)]);

        Assert.Equal(70, evaluacion.Puntaje);
        Assert.Equal(NivelConfianza.Alta, evaluacion.Nivel);
        Assert.Equal(EstadoChecada.Verificada, evaluacion.Estado);
    }

    [Fact]
    public void SoloQrNoBastaYQuedaParaRevision()
    {
        var evaluacion = EvaluadorConfianza.Evaluar([Senal(TipoSenal.CodigoQr)]);

        Assert.Equal(25, evaluacion.Puntaje);
        Assert.Equal(EstadoChecada.RequiereRevision, evaluacion.Estado);
    }

    [Fact]
    public void RepetirElMismoTipoDeSenalNoDuplicaElPuntaje()
    {
        var evaluacion = EvaluadorConfianza.Evaluar(
            [Senal(TipoSenal.CodigoQr), Senal(TipoSenal.CodigoQr), Senal(TipoSenal.CodigoQr)]);

        Assert.Equal(25, evaluacion.Puntaje);
    }

    [Fact]
    public void UnaSenalSospechosaDescuentaDelPuntaje()
    {
        var evaluacion = EvaluadorConfianza.Evaluar(
        [
            Senal(TipoSenal.CodigoQr),
            Senal(TipoSenal.WebAuthn),
            Senal(TipoSenal.Geocerca, ResultadoSenal.Sospechosa)
        ]);

        Assert.Equal(45, evaluacion.Puntaje);
        Assert.Equal(EstadoChecada.RequiereRevision, evaluacion.Estado);
    }

    [Fact]
    public void UnaSenalNoDisponibleNoSumaNiResta()
    {
        var evaluacion = EvaluadorConfianza.Evaluar(
        [
            Senal(TipoSenal.CodigoQr),
            Senal(TipoSenal.WebAuthn),
            Senal(TipoSenal.BeaconBle, ResultadoSenal.NoDisponible)
        ]);

        Assert.Equal(70, evaluacion.Puntaje);
    }

    [Fact]
    public void ElPuntajeNuncaBajaDeCero()
    {
        var evaluacion = EvaluadorConfianza.Evaluar(
        [
            Senal(TipoSenal.CodigoQr, ResultadoSenal.Fallida),
            Senal(TipoSenal.WebAuthn, ResultadoSenal.Sospechosa),
            Senal(TipoSenal.Geocerca, ResultadoSenal.Sospechosa)
        ]);

        Assert.Equal(0, evaluacion.Puntaje);
        Assert.Equal(EstadoChecada.Rechazada, evaluacion.Estado);
    }

    [Fact]
    public void CadaSenalRegistraElPesoQueAporto()
    {
        var qr = Senal(TipoSenal.CodigoQr);
        var fallida = Senal(TipoSenal.Geocerca, ResultadoSenal.Fallida);

        EvaluadorConfianza.Evaluar([qr, fallida]);

        Assert.Equal(25, qr.PesoAplicado);
        Assert.Equal(-10, fallida.PesoAplicado);
    }

    [Fact]
    public void SumarUnTipoNuevoNoObligaACambiarElEvaluador()
    {
        // Ensayo del escenario de fase 2: el beacon ya puntúa sin tocar la lógica.
        var conBeacon = EvaluadorConfianza.Evaluar(
            [Senal(TipoSenal.CodigoQr), Senal(TipoSenal.BeaconBle)]);

        Assert.Equal(45, conBeacon.Puntaje);
        Assert.Equal(NivelConfianza.Media, conBeacon.Nivel);
    }
}
