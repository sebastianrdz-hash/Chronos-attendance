using System.Net.Http.Json;
using Chronos.Api.Contratos;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Api.Tests.Ayudantes;

/// <summary>
/// Da de alta un empleado nuevo y devuelve una sesión suya ya utilizable.
/// <para>
/// Las pruebas de fichaje comparten una sola base, y las reglas del dominio tienen memoria:
/// la ventana antiduplicados de cinco minutos y el estado del día hacen que dos pruebas que
/// usen la misma cuenta semilla se estorben según el orden en que corran. Estrenar empleado
/// en cada prueba sale más barato que descubrir por qué falla solo a veces.
/// </para>
/// </summary>
internal static class AltaDePrueba
{
    public static async Task<SesionDePrueba> NuevoEmpleadoAsync(
        this FabricaApiPruebas fabrica,
        Guid? departamentoId = null,
        Guid? sedeId = null,
        Guid? turnoId = null,
        RolChronos rol = RolChronos.Empleado,
        string prefijo = "prueba")
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var correo = $"{prefijo}.{sufijo}@chronos.mx";

        var respuesta = await admin.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = $"P-{sufijo}",
            Nombres = "Prueba",
            ApellidoPaterno = "Automatizada",
            CorreoCorporativo = correo,
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = departamentoId ?? SembradorDatos.Ids.DeptoSistemas,
            SedeId = sedeId ?? SembradorDatos.Ids.SedeMonterrey,
            TurnoId = turnoId,
            Rol = rol
        }, ClienteAutenticado.Json);

        var alta = await (await respuesta.AsegurarExitoAsync()).LeerAsync<RespuestaAltaEmpleado>();

        var cliente = fabrica.CreateClient();
        var temporal = await ClienteAutenticado.EntrarAsync(cliente, correo, alta.ContrasenaTemporal);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", temporal.AccessToken);

        // Sin quitar el candado de contraseña temporal, cualquier otra llamada rebota.
        var cambio = await cliente.PostAsJsonAsync("/api/v1/perfil/contrasena", new SolicitudCambioContrasena
        {
            ContrasenaActual = alta.ContrasenaTemporal,
            ContrasenaNueva = FabricaApiPruebas.Contrasena,
            Confirmacion = FabricaApiPruebas.Contrasena
        }, ClienteAutenticado.Json);

        await cambio.AsegurarExitoAsync();

        var sesion = await ClienteAutenticado.EntrarAsync(cliente, correo, FabricaApiPruebas.Contrasena);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", sesion.AccessToken);

        return new SesionDePrueba(cliente, alta.Empleado);
    }
}

internal sealed record SesionDePrueba(HttpClient Cliente, EmpleadoDto Empleado);
