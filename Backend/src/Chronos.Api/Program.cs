using System.Text.Json.Serialization;
using Chronos.Api.Endpoints;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Infrastructure;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Render, Fly.io y Cloud Run asignan el puerto en tiempo de despliegue y lo publican
// en PORT. Sin esto la aplicación escucharía en el 8080 fijo y la plataforma la
// declararía caída porque nadie responde donde ella espera.
var puertoAsignado = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(puertoAsignado))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{puertoAsignado}");
}

builder.Host.UseSerilog((contexto, servicios, configuracion) => configuracion
    .ReadFrom.Configuration(contexto.Configuration)
    .ReadFrom.Services(servicios)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacion", "Chronos.Api")
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console());

const string PoliticaCors = "cliente-chronos";

var origenesPermitidos = builder.Configuration
    .GetSection("Cors:OrigenesPermitidos")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(cors => cors.AddPolicy(PoliticaCors, politica => politica
    .WithOrigins(origenesPermitidos)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;

    // De fábrica solo se confía en proxies de loopback. En un PaaS el proxy vive en
    // otra red, así que se vacían las listas para aceptar el salto que ya hizo la
    // plataforma; ella sobrescribe estas cabeceras y no deja que el cliente las falsee.
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
});

builder.Services.AgregarInfraestructura();
builder.Services.AgregarAutenticacionJwt();
builder.Services.AddScoped<IResolutorAcceso, ResolutorAcceso>();

builder.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorSolicitudInvalida>();

builder.Services.AddOpenApi(opciones =>
{
    opciones.AddDocumentTransformer<TransformadorSeguridadOpenApi>();
});

builder.Services.AddHealthChecks()
    .AddNpgSql(
        proveedor => InyeccionDependencias.CadenaDeConexion(
            proveedor.GetRequiredService<IConfiguration>()),
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

// Va antes que todo lo demás: sin traducir X-Forwarded-Proto, la aplicación cree que
// la petición llegó por HTTP plano y UseHttpsRedirection la reenvía a HTTPS una y otra
// vez contra el mismo proxy, que vuelve a entregarla por HTTP.
app.UseForwardedHeaders();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

// La documentación queda expuesta también fuera de desarrollo porque este proyecto es
// una pieza de portafolio y explorar los endpoints es parte de la demostración.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Api:ExponerDocumentacion", false))
{
    app.MapOpenApi();
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Chronos API v1");
        ui.DocumentTitle = "Chronos · API";
        ui.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = chequeo => chequeo.Tags.Contains("ready")
});

app.MapearAutenticacion();
app.MapearMeta();
app.MapearSedes();
app.MapearDepartamentos();
app.MapearTurnos();
app.MapearEmpleados();
app.MapearPerfil();

if (app.Configuration.GetValue("Semilla:EjecutarAlIniciar", true))
{
    await SembradorDatos.SembrarAsync(app.Services);
}

app.Run();

/// <summary>Punto de anclaje para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
