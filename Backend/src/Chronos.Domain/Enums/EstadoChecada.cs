namespace Chronos.Domain.Enums;

public enum EstadoChecada
{
    /// <summary>Aceptada con evidencia suficiente.</summary>
    Verificada = 1,

    /// <summary>Aceptada, pero marcada para que Recursos Humanos la revise.</summary>
    RequiereRevision = 2,

    /// <summary>Sin evidencia utilizable; no cuenta para el cálculo de jornada.</summary>
    Rechazada = 3,

    /// <summary>Un supervisor la validó de forma explícita tras una revisión.</summary>
    AjustadaPorSupervisor = 4
}
