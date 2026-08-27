using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Chronos.Api.Extensiones;

/// <summary>
/// Registra el esquema Bearer en el documento OpenAPI para que Swagger UI
/// ofrezca el botón de autorizar.
/// </summary>
public sealed class TransformadorSeguridadOpenApi : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument documento,
        OpenApiDocumentTransformerContext contexto,
        CancellationToken cancellationToken)
    {
        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Pega aquí el access_token que devuelve /api/v1/auth/login."
        };

        return Task.CompletedTask;
    }
}
