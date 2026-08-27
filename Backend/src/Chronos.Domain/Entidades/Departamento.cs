using Chronos.Domain.Common;

namespace Chronos.Domain.Entidades;

public class Departamento : EntidadBase
{
    public required string Nombre { get; set; }

    public required string Codigo { get; set; }

    public Guid SedeId { get; set; }

    public Sede? Sede { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<Empleado> Empleados { get; set; } = [];
}
