namespace Chronos.Domain.Seguridad;

/// <summary>
/// Devuelve el motivo junto con la decisión para que la API pueda responder un 403
/// explicando qué frontera se cruzó, en vez de un rechazo mudo.
/// </summary>
public readonly record struct ResultadoAcceso(bool Permitido, string? Motivo)
{
    public static ResultadoAcceso Permitir() => new(true, null);

    public static ResultadoAcceso Negar(string motivo) => new(false, motivo);

    public static implicit operator bool(ResultadoAcceso resultado) => resultado.Permitido;
}
