using System.ComponentModel.DataAnnotations;
using Chronos.Domain.Enums;

namespace Chronos.Api.Contratos;

// --- Sede ---

public sealed record SedeDto(
    Guid Id,
    string Nombre,
    string Codigo,
    string? Direccion,
    string ZonaHoraria,
    double? Latitud,
    double? Longitud,
    int? RadioMetros,
    bool Activa,
    int TotalDepartamentos,
    int TotalEmpleados);

public sealed record SolicitudSede
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "El código debe tener entre 2 y 20 caracteres.")]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "El código solo admite mayúsculas, números y guiones.")]
    public string Codigo { get; init; } = string.Empty;

    [StringLength(250, ErrorMessage = "La dirección no puede exceder 250 caracteres.")]
    public string? Direccion { get; init; }

    [Required(ErrorMessage = "La zona horaria es obligatoria.")]
    [StringLength(60)]
    public string ZonaHoraria { get; init; } = "America/Mexico_City";

    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public double? Latitud { get; init; }

    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public double? Longitud { get; init; }

    [Range(10, 5000, ErrorMessage = "El radio debe estar entre 10 y 5000 metros.")]
    public int? RadioMetros { get; init; }

    public bool Activa { get; init; } = true;

    /// <summary>La geocerca es opcional, pero a medias no sirve para nada.</summary>
    public bool GeocercaCompleta => Latitud is not null && Longitud is not null && RadioMetros is not null;

    public bool GeocercaVacia => Latitud is null && Longitud is null && RadioMetros is null;
}

// --- Departamento ---

public sealed record DepartamentoDto(
    Guid Id,
    string Nombre,
    string Codigo,
    Guid SedeId,
    string SedeNombre,
    bool Activo,
    int TotalEmpleados);

public sealed record SolicitudDepartamento
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "El código debe tener entre 2 y 20 caracteres.")]
    [RegularExpression("^[A-Z0-9-]+$", ErrorMessage = "El código solo admite mayúsculas, números y guiones.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una sede.")]
    public Guid SedeId { get; init; }

    public bool Activo { get; init; } = true;
}

// --- Turno ---

public sealed record TurnoDto(
    Guid Id,
    string Nombre,
    TimeOnly HoraEntrada,
    TimeOnly HoraSalida,
    int ToleranciaMinutos,
    int MinutosDescanso,
    IReadOnlyList<string> DiasLaborales,
    bool CruzaMedianoche,
    double HorasProgramadas,
    bool Activo,
    int TotalEmpleados);

public sealed record SolicitudTurno
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "La hora de entrada es obligatoria.")]
    public TimeOnly HoraEntrada { get; init; }

    [Required(ErrorMessage = "La hora de salida es obligatoria.")]
    public TimeOnly HoraSalida { get; init; }

    [Range(0, 120, ErrorMessage = "La tolerancia debe estar entre 0 y 120 minutos.")]
    public int ToleranciaMinutos { get; init; } = 10;

    [Range(0, 240, ErrorMessage = "El descanso debe estar entre 0 y 240 minutos.")]
    public int MinutosDescanso { get; init; } = 60;

    /// <summary>
    /// Se recibe como lista de nombres ("Lunes", "Martes") en vez del entero de banderas:
    /// el contrato queda legible en Swagger y los checkboxes del cliente mapean directo.
    /// </summary>
    [Required(ErrorMessage = "Selecciona al menos un día laboral.")]
    [MinLength(1, ErrorMessage = "Selecciona al menos un día laboral.")]
    public IReadOnlyList<string> DiasLaborales { get; init; } = [];

    public bool Activo { get; init; } = true;
}

public static class DiasLaboralesMapeo
{
    private static readonly DiasSemana[] Individuales =
    [
        DiasSemana.Domingo, DiasSemana.Lunes, DiasSemana.Martes, DiasSemana.Miercoles,
        DiasSemana.Jueves, DiasSemana.Viernes, DiasSemana.Sabado
    ];

    public static IReadOnlyList<string> ANombres(DiasSemana dias) =>
        Individuales.Where(dia => dias.HasFlag(dia)).Select(dia => dia.ToString()).ToArray();

    public static bool Intentar(IReadOnlyList<string> nombres, out DiasSemana dias, out string? error)
    {
        dias = DiasSemana.Ninguno;

        foreach (var nombre in nombres)
        {
            var coincidencia = Individuales.FirstOrDefault(
                dia => string.Equals(dia.ToString(), nombre, StringComparison.OrdinalIgnoreCase));

            if (coincidencia == default)
            {
                error = $"'{nombre}' no es un día válido. Usa Domingo, Lunes, Martes, Miercoles, Jueves, Viernes o Sabado.";
                return false;
            }

            dias |= coincidencia;
        }

        if (dias == DiasSemana.Ninguno)
        {
            error = "Selecciona al menos un día laboral.";
            return false;
        }

        error = null;
        return true;
    }
}
