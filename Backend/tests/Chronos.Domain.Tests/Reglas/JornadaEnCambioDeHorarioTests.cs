using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;
using Chronos.Domain.Tests.Ayudantes;

namespace Chronos.Domain.Tests.Reglas;

/// <summary>
/// El cambio de horario es el caso donde la aritmética ingenua de horas se rompe: dos noches
/// al año el turno nocturno no dura lo que dice el reloj de pared. La decisión de diseño es
/// que el HORARIO se pacta en hora local (un turno de 22:00 a 06:00 empieza a las 22:00
/// aunque esa noche dure siete horas) mientras que las HORAS TRABAJADAS se miden en tiempo
/// real transcurrido. De ahí que la noche que adelanta se pague una hora menos y la que
/// atrasa genere una hora extra, que es exactamente lo que pasó en la realidad.
/// </summary>
public class JornadaEnCambioDeHorarioTests
{
    private static readonly TimeZoneInfo Zona = Constructor.ZonaConVerano;

    [Fact]
    public void LaNocheQueAdelantaElRelojSeTrabajaUnaHoraMenos()
    {
        var turno = Constructor.TurnoNocturno();
        var sabado = Constructor.DiaQueAdelanta.AddDays(-1);

        var resumen = CalculadoraJornada.Calcular(sabado, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, sabado, 22), sabado),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 6), sabado)
        ]);

        // Ocho horas de reloj de pared, siete de reloj de verdad, menos los 45 del descanso.
        Assert.Equal(TimeSpan.FromMinutes(375), resumen.HorasTrabajadas);
        Assert.Equal(TimeSpan.Zero, resumen.HorasExtra);
    }

    [Fact]
    public void QuienCubreLaNocheQueAdelantaNoQuedaComoSalidaAnticipada()
    {
        var turno = Constructor.TurnoNocturno();
        var sabado = Constructor.DiaQueAdelanta.AddDays(-1);

        var resumen = CalculadoraJornada.Calcular(sabado, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, sabado, 22), sabado),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 6), sabado)
        ]);

        // Se fue a las 06:00 que marcaba el reloj, que es a lo que se comprometió. Castigarlo
        // por la hora que el calendario le quitó sería un error de la nómina, no suyo.
        Assert.Equal(0, resumen.MinutosSalidaAnticipada);
        Assert.Equal(0, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
    }

    [Fact]
    public void LaNocheQueAtrasaElRelojGeneraLaHoraExtraQueDeVerasSeTrabajo()
    {
        var turno = Constructor.TurnoNocturno();
        var sabado = Constructor.DiaQueAtrasa.AddDays(-1);

        var resumen = CalculadoraJornada.Calcular(sabado, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, sabado, 22), sabado),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, Constructor.DiaQueAtrasa, 6), sabado)
        ]);

        // Nueve horas reales menos 45 de descanso: una hora por encima de las 7:15 pactadas.
        Assert.Equal(TimeSpan.FromMinutes(495), resumen.HorasTrabajadas);
        Assert.Equal(TimeSpan.FromHours(1), resumen.HorasExtra);
        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
    }

    [Fact]
    public void UnaEntradaProgramadaEnLaHoraQueNoExisteSeRecorreYNoInventaRetardo()
    {
        // 02:30 no existió el día que adelanta: el reloj brincó de 02:00 a 03:00. Sin el
        // manejo explícito, .NET resolvería esa hora fantasma con el desfase equivocado y
        // todo el turno amanecería con una hora de retardo que nadie cometió.
        var turno = TurnoQueEmpiezaEnLaHoraFantasma();

        var resumen = CalculadoraJornada.Calcular(Constructor.DiaQueAdelanta, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 3, 30), Constructor.DiaQueAdelanta),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 10, 30), Constructor.DiaQueAdelanta)
        ]);

        Assert.Equal(0, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
    }

    [Fact]
    public void LlegarTardeSigueContandoAunqueLaEntradaSeHayaRecorrido()
    {
        var turno = TurnoQueEmpiezaEnLaHoraFantasma();

        var resumen = CalculadoraJornada.Calcular(Constructor.DiaQueAdelanta, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 4), Constructor.DiaQueAdelanta),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, Constructor.DiaQueAdelanta, 10, 30), Constructor.DiaQueAdelanta)
        ]);

        Assert.Equal(30, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Retardo, resumen.Estado);
    }

    [Fact]
    public void EnLaHoraQueOcurreDosVecesMandaLaPrimeraPasada()
    {
        var turno = TurnoQueEmpiezaEnLaHoraRepetida();

        var resumen = CalculadoraJornada.Calcular(Constructor.DiaQueAtrasa, turno, Zona,
        [
            Constructor.Checada(
                TipoChecada.Entrada,
                Constructor.LocalEn(Zona, Constructor.DiaQueAtrasa, 1, 30, enHorarioDeVerano: true),
                Constructor.DiaQueAtrasa),
            Constructor.Checada(
                TipoChecada.Salida,
                Constructor.LocalEn(Zona, Constructor.DiaQueAtrasa, 9, 30),
                Constructor.DiaQueAtrasa)
        ]);

        Assert.Equal(0, resumen.MinutosRetardo);
    }

    [Fact]
    public void QuienEntraEnLaSegundaPasadaDeLaHoraRepetidaLlegaUnaHoraTarde()
    {
        var turno = TurnoQueEmpiezaEnLaHoraRepetida();

        var resumen = CalculadoraJornada.Calcular(Constructor.DiaQueAtrasa, turno, Zona,
        [
            Constructor.Checada(
                TipoChecada.Entrada,
                Constructor.LocalEn(Zona, Constructor.DiaQueAtrasa, 1, 30, enHorarioDeVerano: false),
                Constructor.DiaQueAtrasa),
            Constructor.Checada(
                TipoChecada.Salida,
                Constructor.LocalEn(Zona, Constructor.DiaQueAtrasa, 9, 30),
                Constructor.DiaQueAtrasa)
        ]);

        // Su reloj marcaba la misma hora, pero llegó sesenta minutos después. La regla es
        // discutible; lo importante es que sea explícita y no un accidente del framework.
        Assert.Equal(60, resumen.MinutosRetardo);
        Assert.Equal(EstadoAsistencia.Retardo, resumen.Estado);
    }

    [Fact]
    public void ElDiaNormalDeLaMismaZonaNoSeVeAfectadoPorLasReglasDeVerano()
    {
        var turno = Constructor.TurnoMatutino();
        var miercoles = new DateOnly(2026, 8, 26);

        var resumen = CalculadoraJornada.Calcular(miercoles, turno, Zona,
        [
            Constructor.Checada(TipoChecada.Entrada, Constructor.LocalEn(Zona, miercoles, 9), miercoles),
            Constructor.Checada(TipoChecada.Salida, Constructor.LocalEn(Zona, miercoles, 18), miercoles)
        ]);

        Assert.Equal(TimeSpan.FromHours(8), resumen.HorasTrabajadas);
        Assert.Equal(EstadoAsistencia.Completa, resumen.Estado);
    }

    private static Turno TurnoQueEmpiezaEnLaHoraFantasma() => new()
    {
        Nombre = "Madrugada",
        HoraEntrada = new TimeOnly(2, 30),
        HoraSalida = new TimeOnly(10, 30),
        ToleranciaMinutos = 10,
        MinutosDescanso = 45,
        DiasLaborales = DiasSemana.Todos
    };

    private static Turno TurnoQueEmpiezaEnLaHoraRepetida() => new()
    {
        Nombre = "Madrugada",
        HoraEntrada = new TimeOnly(1, 30),
        HoraSalida = new TimeOnly(9, 30),
        ToleranciaMinutos = 10,
        MinutosDescanso = 45,
        DiasLaborales = DiasSemana.Todos
    };
}
