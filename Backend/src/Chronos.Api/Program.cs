using System.Text.Json.Serialization;
using Chronos.Api.Endpoints;
using Chronos.Api.Extensiones;
using Chronos.Api.Seguridad;
using Chronos.Infrastructure;
using Chronos.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

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

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Chronos API v1");
        ui.DocumentTitle = "Chronos · API";
        ui.RoutePrefix = "swagger";
    });
}
else
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
