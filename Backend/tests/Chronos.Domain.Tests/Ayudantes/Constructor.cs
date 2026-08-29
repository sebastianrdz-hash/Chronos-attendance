using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Tests.Ayudantes;

internal static class Constructor
{
    /// <summary>
    /// Zona fija de -06:00 en vez de una IANA real: los cálculos quedan deterministas
    /// sin depender de la base de husos horarios del sistema operativo.
    /// </summary>
    public static readonly TimeZoneInfo ZonaCentro = TimeZoneInfo.CreateCustomTimeZone(
        "Chronos/Pruebas-06",
        TimeSpan.FromHours(-6),
        "Centro de México (prueba)",
        "Centro de México (prueba)");

    /// <summary>
    /// Zona con horario de verano propia en vez de una IANA real. México lo abolió en 2022,
    /// así que ninguna zona mexicana sirve ya para probar el salto; y amarrar la prueba a
    /// "America/Los_Angeles" la volvería rehén de las actualizaciones de la base de husos
    /// del sistema operativo. Las reglas son las de Estados Unidos: adelanta el segundo
    /// domingo de marzo a las 02:00 y atrasa el primer domingo de noviembre.
    /// </summary>
    public static readonly TimeZoneInfo ZonaConVerano = CrearZonaConVerano();

    /// <summary>Segundo domingo de marzo de 2026: a las 02:00 el reloj salta a las 03:00.</summary>
    public static readonly DateOnly DiaQueAdelanta = new(2026, 3, 8);

    /// <summary>Primer domingo de noviembre de 2026: a las 02:00 el reloj regresa a la 01:00.</summary>
    public static readonly DateOnly DiaQueAtrasa = new(2026, 11, 1);

    private static TimeZoneInfo CrearZonaConVerano()
    {
        var alasDos = new DateTime(1, 1, 1, 2, 0, 0);

        var regla = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(alasDos, month: 3, week: 2, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(alasDos, month: 11, week: 1, DayOfWeek.Sunday));

        return TimeZoneInfo.CreateCustomTimeZone(
            "Chronos/Pruebas-verano",
            TimeSpan.FromHours(-6),
            "Zona con verano (prueba)",
            "Estándar (prueba)",
            "Verano (prueba)",
            [regla]);
    }

    public static Turno TurnoMatutino() => new()
    {
        Nombre = "Matutino",
        HoraEntrada = new TimeOnly(9, 0),
        HoraSalida = new TimeOnly(18, 0),
        ToleranciaMinutos = 10,
        MinutosDescanso = 60,
        DiasLaborales = DiasSemana.LunesAViernes
    };

    public static Turno TurnoNocturno() => new()
    {
        Nombre = "Nocturno",
        HoraEntrada = new TimeOnly(22, 0),
        HoraSalida = new TimeOnly(6, 0),
        ToleranciaMinutos = 15,
        MinutosDescanso = 45,
        DiasLaborales = DiasSemana.LunesASabado
    };

    /// <summary>Convierte una hora local de la zona de prueba a su instante UTC.</summary>
    public static DateTimeOffset Local(DateOnly dia, int hora, int minuto = 0) =>
        new(dia.ToDateTime(new TimeOnly(hora, minuto)), TimeSpan.FromHours(-6));

    /// <summary>
    /// Igual que <see cref="Local"/> pero resolviendo el desfase con la zona indicada, que es
    /// lo que hace falta cuando el día cambia de horario a media jornada. Para la hora que
    /// ocurre dos veces hay que decir cuál: <paramref name="enHorarioDeVerano"/> escoge la
    /// primera pasada.
    /// </summary>
    public static DateTimeOffset LocalEn(TimeZoneInfo zona, DateOnly dia, int hora, int minuto = 0, bool enHorarioDeVerano = false)
    {
        var local = dia.ToDateTime(new TimeOnly(hora, minuto));

        var desfase = zona.IsAmbiguousTime(local)
            ? (enHorarioDeVerano
                ? zona.GetAmbiguousTimeOffsets(local).Max()
                : zona.GetAmbiguousTimeOffsets(local).Min())
            : zona.GetUtcOffset(local);

        return new DateTimeOffset(local, desfase).ToUniversalTime();
    }

    public static Checada Checada(
        TipoChecada tipo,
        DateTimeOffset momento,
        DateOnly dia,
        params (TipoSenal Tipo, ResultadoSenal Resultado)[] senales)
    {
        var checada = new Checada
        {
            EmpleadoId = Guid.CreateVersion7(),
            Tipo = tipo,
            MomentoUtc = momento,
            DiaLaboral = dia
        };

        var aplicar = senales.Length > 0
            ? senales
            : [(TipoSenal.CodigoQr, ResultadoSenal.Confirmada), (TipoSenal.WebAuthn, ResultadoSenal.Confirmada)];

        foreach (var (tipoSenal, resultado) in aplicar)
        {
            checada.AgregarSenal(tipoSenal, resultado);
        }

        return checada;
    }
}
