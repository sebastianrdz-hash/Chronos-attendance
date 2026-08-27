using Chronos.Domain.Common;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;

namespace Chronos.Domain.Entidades;

/// <summary>
/// Un fichaje. No es un booleano: es un momento respaldado por un conjunto de señales
/// cuya suma determina qué tanto se puede confiar en él.
/// </summary>
public class Checada : EntidadBase
{
    public Guid EmpleadoId { get; set; }

    public Empleado? Empleado { get; set; }

    public TipoChecada Tipo { get; set; }

    public DateTimeOffset MomentoUtc { get; set; }

    /// <summary>
    /// Día al que se imputa el fichaje según la zona horaria de la sede. Se guarda
    /// resuelto para que los turnos nocturnos no queden partidos entre dos fechas.
    /// </summary>
    public DateOnly DiaLaboral { get; set; }

    public Guid? SedeId { get; set; }

    public Sede? Sede { get; set; }

    public EstadoChecada Estado { get; private set; } = EstadoChecada.Rechazada;

    public int PuntajeConfianza { get; private set; }

    public NivelConfianza NivelConfianza { get; private set; } = NivelConfianza.Nula;

    public string? HuellaDispositivo { get; set; }

    public string? DireccionIp { get; set; }

    public string? Observaciones { get; set; }

    public Guid? AjustadaPorUsuarioId { get; private set; }

    public string? MotivoAjuste { get; private set; }

    public ICollection<SenalPresencia> Senales { get; set; } = [];

    public SenalPresencia AgregarSenal(
        TipoSenal tipo,
        ResultadoSenal resultado,
        string? detalleJson = null,
        DateTimeOffset? capturadaUtc = null)
    {
        var senal = new SenalPresencia
        {
            ChecadaId = Id,
            Tipo = tipo,
            Resultado = resultado,
            DetalleJson = detalleJson,
            CapturadaUtc = capturadaUtc ?? DateTimeOffset.UtcNow
        };

        Senales.Add(senal);
        Reevaluar();

        return senal;
    }

    public EvaluacionConfianza Reevaluar(PoliticaConfianza? politica = null)
    {
        var evaluacion = EvaluadorConfianza.Evaluar(Senales, politica);

        PuntajeConfianza = evaluacion.Puntaje;
        NivelConfianza = evaluacion.Nivel;

        if (Estado != EstadoChecada.AjustadaPorSupervisor)
        {
            Estado = evaluacion.Estado;
        }

        return evaluacion;
    }

    public void AjustarPorSupervisor(Guid usuarioId, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        Estado = EstadoChecada.AjustadaPorSupervisor;
        AjustadaPorUsuarioId = usuarioId;
        MotivoAjuste = motivo;
        ActualizadoUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Las checadas rechazadas no alimentan el cálculo de jornada.</summary>
    public bool CuentaParaJornada =>
        Estado is EstadoChecada.Verificada
            or EstadoChecada.RequiereRevision
            or EstadoChecada.AjustadaPorSupervisor;
}
