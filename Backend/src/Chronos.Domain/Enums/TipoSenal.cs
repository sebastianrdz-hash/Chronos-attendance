namespace Chronos.Domain.Enums;

/// <summary>
/// Cada valor representa una forma independiente de corroborar presencia.
/// Los valores son estables y persistidos: nunca se reasignan ni reordenan.
/// </summary>
public enum TipoSenal
{
    CodigoQr = 1,
    WebAuthn = 2,
    BeaconBle = 3,
    Geocerca = 4,
    RedWifi = 5,
    RegistroManual = 6
}
