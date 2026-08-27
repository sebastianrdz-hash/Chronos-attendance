namespace Chronos.Domain.Enums;

public enum ResultadoSenal
{
    /// <summary>La señal se capturó y validó correctamente.</summary>
    Confirmada = 1,

    /// <summary>Se intentó capturar y la validación falló (QR vencido, firma inválida).</summary>
    Fallida = 2,

    /// <summary>No se pudo capturar: el dispositivo o el entorno no la soportan.</summary>
    NoDisponible = 3,

    /// <summary>Válida en lo técnico pero incoherente con el resto (RSSI imposible, GPS lejano).</summary>
    Sospechosa = 4
}
