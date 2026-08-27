using System.ComponentModel.DataAnnotations;
using Chronos.Domain.Seguridad;

namespace Chronos.Api.Extensiones;

public static class Validacion
{
    /// <summary>
    /// Agrupa los errores por nombre de propiedad para que la respuesta encaje con el
    /// formato de ValidationProblemDetails y el formulario del cliente pueda pintarlos
    /// campo por campo.
    /// </summary>
    public static bool Intentar<T>(T instancia, out Dictionary<string, string[]> errores) where T : notnull
    {
        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(instancia);

        if (Validator.TryValidateObject(instancia, contexto, resultados, validateAllProperties: true))
        {
            errores = [];
            return true;
        }

        errores = resultados
            .SelectMany(
                resultado => resultado.MemberNames.DefaultIfEmpty(string.Empty),
                (resultado, miembro) => (Miembro: miembro, resultado.ErrorMessage))
            .GroupBy(par => par.Miembro, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(par => par.ErrorMessage ?? "Valor no válido.").ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return false;
    }

    /// <summary>Atajo para los errores que solo se pueden detectar consultando la base.</summary>
    public static IResult ProblemaDeCampo(string campo, string mensaje) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [campo] = [mensaje] });
}

/// <summary>
/// Valida el cuerpo de la petición antes de que corra el manejador. Se aplica con
/// <c>.ConValidacion&lt;T&gt;()</c> para no repetir el bloque de comprobación en cada endpoint.
/// </summary>
internal sealed class FiltroValidacion<T> : IEndpointFilter where T : notnull
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var modelo = contexto.Arguments.OfType<T>().FirstOrDefault();

        if (modelo is not null && !Validacion.Intentar(modelo, out var errores))
        {
            return TypedResults.ValidationProblem(errores);
        }

        return await siguiente(contexto);
    }
}

public static class ExtensionesEndpoint
{
    public static RouteHandlerBuilder ConValidacion<T>(this RouteHandlerBuilder constructor) where T : notnull =>
        constructor
            .AddEndpointFilter<FiltroValidacion<T>>()
            .ProducesValidationProblem();

    /// <summary>
    /// Convierte una decisión del dominio en un 403 con el motivo dentro. Devuelve null
    /// cuando la operación está permitida, para poder escribir
    /// <c>if (Acceso.Rechazo(decision) is { } alto) return alto;</c>
    /// </summary>
    public static IResult? Rechazo(this ResultadoAcceso decision) =>
        decision.Permitido
            ? null
            : TypedResults.Problem(
                detail: decision.Motivo,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Acceso denegado");
}
