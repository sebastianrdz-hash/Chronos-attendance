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

        // Un dictamen humano manda sobre el puntaje. Se comprueba por quién decidió y no
        // por el estado resultante, porque revisar también puede terminar en rechazo y ese
        // estado es indistinguible del que produciría una evaluación automática.
        if (AjustadaPorUsuarioId is null)
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

    /// <summary>
    /// Descarta el fichaje tras una revisión. La checada no se borra: deja de contar para
    /// la jornada pero sigue ahí, con el motivo y el nombre de quien decidió, porque el
    /// registro de lo que se rechazó importa tanto como el de lo que se aceptó.
    /// </summary>
    public void RechazarPorSupervisor(Guid usuarioId, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        Estado = EstadoChecada.Rechazada;
        AjustadaPorUsuarioId = usuarioId;
        MotivoAjuste = motivo;
        ActualizadoUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Una checada ya resuelta no vuelve a la bandeja. Sin esto, dos revisores podrían
    /// pisarse el dictamen sin enterarse.
    /// </summary>
    public bool EsperaRevision => Estado == EstadoChecada.RequiereRevision;

    /// <summary>Las checadas rechazadas no alimentan el cálculo de jornada.</summary>
    public bool CuentaParaJornada =>
        Estado is EstadoChecada.Verificada
            or EstadoChecada.RequiereRevision
            or EstadoChecada.AjustadaPorSupervisor;
}
