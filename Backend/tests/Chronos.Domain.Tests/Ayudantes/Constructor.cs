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
