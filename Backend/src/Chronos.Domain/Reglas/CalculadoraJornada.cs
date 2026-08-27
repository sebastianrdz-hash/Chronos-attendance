using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Reglas;

/// <summary>
/// Convierte las checadas de un día en horas trabajadas, retardo y tiempo extra.
/// Es puro a propósito: recibe todo lo que necesita y no toca base de datos ni reloj,
/// de modo que cada regla se pueda probar de forma aislada.
/// </summary>
public static class CalculadoraJornada
{
    public static ResumenJornada Calcular(
        DateOnly dia,
        Turno turno,
        TimeZoneInfo zona,
        IEnumerable<Checada> checadas)
    {
        ArgumentNullException.ThrowIfNull(turno);
        ArgumentNullException.ThrowIfNull(zona);

        var validas = checadas
            .Where(c => c.DiaLaboral == dia && c.CuentaParaJornada)
            .OrderBy(c => c.MomentoUtc)
            .ToList();

        if (!turno.EsDiaLaboral(dia.DayOfWeek) && validas.Count == 0)
        {
            return new ResumenJornada { Dia = dia, Estado = EstadoAsistencia.Descanso };
        }

        var entrada = validas.FirstOrDefault(c => c.Tipo == TipoChecada.Entrada);
        var salida = validas.LastOrDefault(c => c.Tipo == TipoChecada.Salida);

        var requiereRevision = validas.Any(c => c.Estado == EstadoChecada.RequiereRevision);
        var confianzaMinima = validas.Count == 0
            ? NivelConfianza.Nula
            : validas.Min(c => c.NivelConfianza);

        if (entrada is null)
        {
            return new ResumenJornada
            {
                Dia = dia,
                Estado = EstadoAsistencia.Falta,
                RequiereRevision = requiereRevision,
                ConfianzaMinima = confianzaMinima
            };
        }

        var inicioProgramado = AUtc(dia.ToDateTime(turno.HoraEntrada), zona);
        var finProgramado = AUtc(
            dia.ToDateTime(turno.HoraSalida).AddDays(turno.CruzaMedianoche ? 1 : 0),
            zona);

        var minutosRetardo = CalcularRetardo(entrada.MomentoUtc, inicioProgramado, turno.ToleranciaMinutos);

        if (salida is null)
        {
            return new ResumenJornada
            {
                Dia = dia,
                Entrada = entrada.MomentoUtc,
                MinutosRetardo = minutosRetardo,
                Estado = EstadoAsistencia.JornadaIncompleta,
                RequiereRevision = true,
                ConfianzaMinima = confianzaMinima
            };
        }

        var bruto = salida.MomentoUtc - entrada.MomentoUtc;
        var descanso = DescansoAplicable(validas, turno, bruto);
        var trabajadas = bruto - descanso;

        var extra = trabajadas > turno.DuracionProgramada
            ? trabajadas - turno.DuracionProgramada
            : TimeSpan.Zero;

        var minutosSalidaAnticipada = salida.MomentoUtc < finProgramado
            ? (int)Math.Round((finProgramado - salida.MomentoUtc).TotalMinutes)
            : 0;

        return new ResumenJornada
        {
            Dia = dia,
            Entrada = entrada.MomentoUtc,
            Salida = salida.MomentoUtc,
            HorasTrabajadas = trabajadas,
            HorasExtra = extra,
            TiempoDescanso = descanso,
            MinutosRetardo = minutosRetardo,
            MinutosSalidaAnticipada = minutosSalidaAnticipada,
            Estado = DeterminarEstado(minutosRetardo, minutosSalidaAnticipada),
            RequiereRevision = requiereRevision,
            ConfianzaMinima = confianzaMinima
        };
    }

    /// <summary>
    /// Dentro de la tolerancia no hay retardo; una vez rebasada se cuenta desde la hora
    /// programada de entrada, no desde el final de la tolerancia.
    /// </summary>
    private static int CalcularRetardo(
        DateTimeOffset entrada,
        DateTimeOffset inicioProgramado,
        int toleranciaMinutos)
    {
        var retraso = entrada - inicioProgramado;

        return retraso.TotalMinutes <= toleranciaMinutos
            ? 0
            : (int)Math.Round(retraso.TotalMinutes);
    }

    /// <summary>
    /// El descanso del turno se descuenta aunque nadie lo haya fichado, para que una
    /// jornada normal no aparezca como tiempo extra. Si el descanso registrado fue mayor,
    /// manda ese. Nunca supera el tiempo total entre entrada y salida.
    /// </summary>
    private static TimeSpan DescansoAplicable(
        IReadOnlyList<Checada> checadas,
        Turno turno,
        TimeSpan bruto)
    {
        var registrado = CalcularDescanso(checadas);
        var programado = TimeSpan.FromMinutes(turno.MinutosDescanso);
        var aplicable = registrado > programado ? registrado : programado;

        return aplicable > bruto ? bruto : aplicable;
    }

    private static TimeSpan CalcularDescanso(IReadOnlyList<Checada> checadas)
    {
        var total = TimeSpan.Zero;
        DateTimeOffset? inicio = null;

        foreach (var checada in checadas)
        {
            switch (checada.Tipo)
            {
                case TipoChecada.InicioDescanso:
                    inicio ??= checada.MomentoUtc;
                    break;

                case TipoChecada.FinDescanso when inicio is not null:
                    total += checada.MomentoUtc - inicio.Value;
                    inicio = null;
                    break;
            }
        }

        return total;
    }

    private static EstadoAsistencia DeterminarEstado(int minutosRetardo, int minutosSalidaAnticipada) =>
        (minutosRetardo, minutosSalidaAnticipada) switch
        {
            ( > 0, _) => EstadoAsistencia.Retardo,
            (_, > 0) => EstadoAsistencia.SalidaAnticipada,
            _ => EstadoAsistencia.Completa
        };

    /// <summary>
    /// Resuelve la hora local a UTC contemplando los saltos de horario de verano:
    /// una hora inexistente se recorre y una ambigua toma el desfase mayor.
    /// </summary>
    private static DateTimeOffset AUtc(DateTime local, TimeZoneInfo zona)
    {
        if (zona.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var desfase = zona.IsAmbiguousTime(local)
            ? zona.GetAmbiguousTimeOffsets(local).Max()
            : zona.GetUtcOffset(local);

        return new DateTimeOffset(local, desfase).ToUniversalTime();
    }
}
