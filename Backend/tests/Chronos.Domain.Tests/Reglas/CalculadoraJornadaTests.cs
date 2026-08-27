using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;
using Chronos.Domain.Tests.Ayudantes;

namespace Chronos.Domain.Tests.Reglas;

public class CalculadoraJornadaTests
{
    private static readonly DateOnly Miercoles = new(2026, 8, 26);
    private static readonly DateOnly Domingo = new(2026, 8, 30);

    [Fact]
    public void JornadaPuntualNoGeneraRetardoNiTiempoExtra()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
        Assert.Equal(0, resumen.MinutosRetardo);
        Assert.Equal(TimeSpan.FromHours(8), resumen.HorasTrabajadas);
        Assert.Equal(TimeSpan.Zero, resumen.HorasExtra);
    }

    [Fact]
    public void LlegarDentroDeLaToleranciaNoCuentaComoRetardo()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9, 8), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(0, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
    }

    [Fact]
    public void RebasarLaToleranciaCuentaElRetardoDesdeLaHoraProgramada()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9, 25), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(25, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Retardo, resumen.Estado);
    }

    [Fact]
    public void QuedarseDeMasGeneraTiempoExtra()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 20), Miercoles)
        ]);

        Assert.Equal(TimeSpan.FromHours(10), resumen.HorasTrabajadas);
        Assert.Equal(TimeSpan.FromHours(2), resumen.HorasExtra);
    }

    [Fact]
    public void SalirAntesDeLaHoraSeReportaComoSalidaAnticipada()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 16, 30), Miercoles)
        ]);

        Assert.Equal(EstadoAsistencia.SalidaAnticipada, resumen.Estado);
        Assert.Equal(90, resumen.MinutosSalidaAnticipada);
    }

    [Fact]
    public void ElDescansoDelTurnoSeDescuentaAunqueNadieLoFiche()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(TimeSpan.FromHours(1), resumen.TiempoDescanso);
    }

    [Fact]
    public void UnDescansoMasLargoQueElProgramadoSeRespeta()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles),
            Constructor.Checada(TipoChecada.InicioDescanso, Constructor.Local(Miercoles, 14), Miercoles),
            Constructor.Checada(TipoChecada.FinDescanso, Constructor.Local(Miercoles, 15, 30), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(TimeSpan.FromMinutes(90), resumen.TiempoDescanso);
        Assert.Equal(TimeSpan.FromMinutes(450), resumen.HorasTrabajadas);
        Assert.Equal(TimeSpan.Zero, resumen.HorasExtra);
    }

    [Fact]
    public void SinEntradaElDiaLaboralCuentaComoFalta()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro, []);

        Assert.Equal(EstadoAsistencia.Falta, resumen.Estado);
        Assert.Null(resumen.Entrada);
    }

    [Fact]
    public void UnDiaNoLaboralSinChecadasEsDescanso()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Domingo, turno, Constructor.ZonaCentro, []);

        Assert.Equal(EstadoAsistencia.Descanso, resumen.Estado);
    }

    [Fact]
    public void EntrarSinRegistrarSalidaDejaLaJornadaIncompletaYParaRevision()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles)
        ]);

        Assert.Equal(EstadoAsistencia.JornadaIncompleta, resumen.Estado);
        Assert.True(resumen.RequiereRevision);
    }

    [Fact]
    public void ElTurnoNocturnoCruzaLaMedianocheSinPartirLaJornada()
    {
        var turno = Constructor.TurnoNocturno();
        var siguiente = Miercoles.AddDays(1);

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 22), Miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(siguiente, 6), Miercoles)
        ]);

        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
        Assert.Equal(0, resumen.MinutosRetardo);
        Assert.Equal(TimeSpan.FromMinutes(435), resumen.HorasTrabajadas);
    }

    [Fact]
    public void UnaChecadaRechazadaNoAlimentaElCalculo()
    {
        var turno = Constructor.TurnoMatutino();

        var entradaSinEvidencia = Constructor.Checada(
            TipoChecada.Entrada,
            Constructor.Local(Miercoles, 9),
            Miercoles,
            (TipoSenal.CodigoQr, ResultadoSenal.Fallida));

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            entradaSinEvidencia,
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.Equal(EstadoChecada.Rechazada, entradaSinEvidencia.Estado);
        Assert.Equal(EstadoAsistencia.Falta, resumen.Estado);
    }

    [Fact]
    public void UnaChecadaDebilMarcaElDiaParaRevision()
    {
        var turno = Constructor.TurnoMatutino();

        var resumen = CalculadoraJornada.Calcular(Miercoles, turno, Constructor.ZonaCentro,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.Local(Miercoles, 9), Miercoles,
                (TipoSenal.CodigoQr, ResultadoSenal.Confirmada)),
            Constructor.Checada(TipoChecada.Salida, Constructor.Local(Miercoles, 18), Miercoles)
        ]);

        Assert.True(resumen.RequiereRevision);
        Assert.Equal(NivelConfianza.Baja, resumen.ConfianzaMinima);
    }

    [Fact]
    public void LasZonasHorariasIanaSeResuelvenEnEsteEntorno()
    {
        // Depende de ICU: si alguien activara InvariantGlobalization, este test lo delata.
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Monterrey");

        Assert.NotNull(zona);
    }
}
