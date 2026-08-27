using Microsoft.AspNetCore.Mvc;

namespace Chronos.Api.Contratos;

/// <summary>
/// Parámetros comunes de todas las listas, enlazados desde la query string con
/// [AsParameters].
///
/// Todo es anulable a propósito: el enlazador de Minimal APIs considera obligatorio
/// cualquier tipo de valor no anulable y hace estallar la petición con un 500 si falta,
/// sin mirar el inicializador de la propiedad. Los valores por omisión se aplican en
/// <see cref="Normalizar"/>.
/// </summary>
public sealed record ParametrosConsulta
{
    [FromQuery(Name = "pagina")]
    public int? Pagina { get; init; }

    [FromQuery(Name = "tamano")]
    public int? Tamano { get; init; }

    /// <summary>Texto libre; cada endpoint decide sobre qué columnas busca.</summary>
    [FromQuery(Name = "buscar")]
    public string? Buscar { get; init; }

    [FromQuery(Name = "ordenarPor")]
    public string? OrdenarPor { get; init; }

    [FromQuery(Name = "descendente")]
    public bool? Descendente { get; init; }

    /// <summary>null = todos, true = solo activos, false = solo inactivos.</summary>
    [FromQuery(Name = "activo")]
    public bool? Activo { get; init; }

    /// <summary>
    /// Se saneia en lugar de rechazar: una página fuera de rango o un tamaño absurdo son
    /// un error del cliente que no vale un 400, pero un tamaño ilimitado sí tumbaría la API.
    /// </summary>
    public ConsultaNormalizada Normalizar() => new(
        Pagina is null or < 1 ? 1 : Pagina.Value,
        Tamano switch
        {
            null or < 1 => ConsultaNormalizada.TamanoPredeterminado,
            > ConsultaNormalizada.TamanoMaximo => ConsultaNormalizada.TamanoMaximo,
            _ => Tamano.Value
        },
        string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim(),
        string.IsNullOrWhiteSpace(OrdenarPor) ? null : OrdenarPor.Trim(),
        Descendente ?? false,
        Activo);
}

public sealed record ConsultaNormalizada(
    int Pagina,
    int Tamano,
    string? Buscar,
    string? OrdenarPor,
    bool Descendente,
    bool? Activo)
{
    public const int TamanoPredeterminado = 20;
    public const int TamanoMaximo = 100;

    public int Salto => (Pagina - 1) * Tamano;
}

public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Elementos, int Pagina, int Tamano, int Total)
{
    public int TotalPaginas => Tamano <= 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;

    public bool HayPaginaAnterior => Pagina > 1;
}
