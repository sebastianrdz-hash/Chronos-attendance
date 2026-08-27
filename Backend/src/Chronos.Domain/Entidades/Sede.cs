using Chronos.Domain.Common;
using Chronos.Domain.ValueObjects;

namespace Chronos.Domain.Entidades;

public class Sede : EntidadBase
{
    public required string Nombre { get; set; }

    public required string Codigo { get; set; }

    public string? Direccion { get; set; }

    /// <summary>
    /// Identificador IANA (por ejemplo "America/Mexico_City"). Todo se persiste en UTC;
    /// esta zona se usa para decidir a qué día laboral pertenece un fichaje.
    /// </summary>
    public string ZonaHoraria { get; set; } = "America/Mexico_City";

    public Geocerca? Geocerca { get; set; }

    public bool Activa { get; set; } = true;

    public ICollection<Departamento> Departamentos { get; set; } = [];

    public ICollection<Empleado> Empleados { get; set; } = [];
}
