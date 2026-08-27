using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Chronos.Api.Extensiones;

/// <summary>
/// Un cuerpo o una query string que no se pueden enlazar hacen que Minimal APIs lance
/// <see cref="BadHttpRequestException"/>, y el manejador por omisión la trata como un
/// fallo del servidor. La excepción ya trae el código correcto: basta con respetarlo
/// para que el cliente reciba un 400 y no un 500 engañoso.
/// </summary>
internal sealed class ManejadorSolicitudInvalida(IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken ct)
    {
        if (excepcion is not BadHttpRequestException solicitudInvalida)
        {
            return false;
        }

        contexto.Response.StatusCode = solicitudInvalida.StatusCode;

        // El mensaje externo solo dice qué parámetro falló; el interno señala el campo y
        // la posición concretos, que es lo único accionable para quien llama.
        var causa = solicitudInvalida.InnerException?.Message;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = solicitudInvalida,
            ProblemDetails = new ProblemDetails
            {
                Status = solicitudInvalida.StatusCode,
                Title = "Solicitud mal formada",
                Detail = causa is null
                    ? solicitudInvalida.Message
                    : $"{solicitudInvalida.Message} {causa}"
            }
        });
    }
}
