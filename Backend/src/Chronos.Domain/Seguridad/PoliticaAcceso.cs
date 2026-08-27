using Chronos.Domain.Enums;

namespace Chronos.Domain.Seguridad;

/// <summary>
/// Reglas de quién puede hacer qué. Son funciones puras para que la API y las pruebas
/// consulten exactamente la misma lógica y no puedan divergir.
///
/// Resumen:
///   Admin      todo.
///   Supervisor lectura global, escritura solo sobre empleados de su departamento.
///   Empleado   únicamente su propio expediente.
/// </summary>
public static class PoliticaAcceso
{
    private const string SoloAdministra = "Solo un administrador puede realizar esta operación.";
    private const string FueraDeSuDepartamento = "Un supervisor solo puede modificar empleados de su propio departamento.";
    private const string SoloSusDatos = "Un empleado solo puede consultar su propio expediente.";
    private const string SupervisorSinDepartamento = "El supervisor no tiene un departamento asignado.";

    public static ResultadoAcceso PuedeListarEmpleados(ContextoAcceso contexto) =>
        contexto.EsAdmin || contexto.EsSupervisor
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar(SoloSusDatos);

    public static ResultadoAcceso PuedeVerEmpleado(ContextoAcceso contexto, Guid empleadoId)
    {
        if (contexto.EsAdmin || contexto.EsSupervisor)
        {
            return ResultadoAcceso.Permitir();
        }

        return contexto.EmpleadoId == empleadoId
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar(SoloSusDatos);
    }

    public static ResultadoAcceso PuedeCrearEmpleado(ContextoAcceso contexto, Guid departamentoDestino)
    {
        if (contexto.EsAdmin)
        {
            return ResultadoAcceso.Permitir();
        }

        if (!contexto.EsSupervisor)
        {
            return ResultadoAcceso.Negar(SoloAdministra);
        }

        return AlcanceDelSupervisor(contexto, departamentoDestino);
    }

    /// <summary>
    /// Se piden los dos departamentos porque mover a alguien fuera del ámbito propio es
    /// tan delicado como traerlo desde fuera: el supervisor necesita mandar en ambos.
    /// </summary>
    public static ResultadoAcceso PuedeEditarEmpleado(
        ContextoAcceso contexto,
        Guid departamentoActual,
        Guid departamentoDestino)
    {
        if (contexto.EsAdmin)
        {
            return ResultadoAcceso.Permitir();
        }

        if (!contexto.EsSupervisor)
        {
            return ResultadoAcceso.Negar(SoloAdministra);
        }

        var sobreElActual = AlcanceDelSupervisor(contexto, departamentoActual);
        return sobreElActual.Permitido
            ? AlcanceDelSupervisor(contexto, departamentoDestino)
            : sobreElActual;
    }

    public static ResultadoAcceso PuedeCambiarEstadoEmpleado(ContextoAcceso contexto, Guid departamentoActual) =>
        PuedeEditarEmpleado(contexto, departamentoActual, departamentoActual);

    /// <summary>
    /// Asignar roles queda reservado al administrador: si un supervisor pudiera hacerlo,
    /// se ascendería a sí mismo o fabricaría supervisores de otros departamentos.
    /// </summary>
    public static ResultadoAcceso PuedeAsignarRol(ContextoAcceso contexto, RolChronos rolDestino)
    {
        if (contexto.EsAdmin)
        {
            return ResultadoAcceso.Permitir();
        }

        return rolDestino == RolChronos.Empleado && contexto.EsSupervisor
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar("Solo un administrador puede otorgar los roles de supervisor o administrador.");
    }

    public static ResultadoAcceso PuedeConsultarCatalogos(ContextoAcceso contexto) =>
        contexto.EsAdmin || contexto.EsSupervisor
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar("Los catálogos solo están disponibles para administradores y supervisores.");

    public static ResultadoAcceso PuedeAdministrarCatalogos(ContextoAcceso contexto) =>
        contexto.EsAdmin
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar(SoloAdministra);

    /// <summary>
    /// Única excepción de escritura para el supervisor fuera de los empleados: los datos
    /// de su propio departamento. Crear o eliminar departamentos sigue siendo del admin.
    /// </summary>
    public static ResultadoAcceso PuedeEditarDepartamento(ContextoAcceso contexto, Guid departamentoId)
    {
        if (contexto.EsAdmin)
        {
            return ResultadoAcceso.Permitir();
        }

        return contexto.EsSupervisor
            ? AlcanceDelSupervisor(contexto, departamentoId)
            : ResultadoAcceso.Negar(SoloAdministra);
    }

    private static ResultadoAcceso AlcanceDelSupervisor(ContextoAcceso contexto, Guid departamentoId)
    {
        if (contexto.DepartamentoId is null)
        {
            return ResultadoAcceso.Negar(SupervisorSinDepartamento);
        }

        return contexto.DepartamentoId == departamentoId
            ? ResultadoAcceso.Permitir()
            : ResultadoAcceso.Negar(FueraDeSuDepartamento);
    }
}
