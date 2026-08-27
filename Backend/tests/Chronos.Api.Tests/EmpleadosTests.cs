using System.Net;
using System.Net.Http.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class EmpleadosTests(FabricaApiPruebas fabrica)
{
    private static int _consecutivo;

    [Fact]
    public async Task LaSemillaDejaQuinceEmpleadosRepartidos()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var pagina = await (await cliente.GetAsync("/api/v1/empleados?tamano=100"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>();

        Assert.True(pagina.Total >= 15, $"Se esperaban al menos 15 empleados y hay {pagina.Total}.");
        Assert.Equal(2, pagina.Elementos.Select(empleado => empleado.SedeNombre).Distinct().Count());
        Assert.Equal(4, pagina.Elementos.Select(empleado => empleado.DepartamentoNombre).Distinct().Count());
    }

    [Fact]
    public async Task ElFiltroDeActivosSeparaLasBajas()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var inactivos = await (await cliente.GetAsync("/api/v1/empleados?activo=false&tamano=100"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>();

        Assert.NotEmpty(inactivos.Elementos);
        Assert.All(inactivos.Elementos, empleado => Assert.False(empleado.Activo));
    }

    [Fact]
    public async Task SePuedeFiltrarPorDepartamento()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var pagina = await (await cliente.GetAsync(
                $"/api/v1/empleados?departamentoId={SembradorDatos.Ids.DeptoOperaciones}&tamano=100"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>();

        Assert.NotEmpty(pagina.Elementos);
        Assert.All(pagina.Elementos, empleado =>
            Assert.Equal(SembradorDatos.Ids.DeptoOperaciones, empleado.DepartamentoId));
    }

    [Fact]
    public async Task ElAltaCreaLaCuentaConContrasenaTemporalQueDebeCambiarse()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(cliente, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        Assert.NotEmpty(alta.ContrasenaTemporal);
        Assert.True(alta.Empleado.DebeCambiarContrasena);
        Assert.Equal(RolChronos.Empleado, alta.Empleado.Rol);

        var sesion = await ClienteAutenticado.EntrarAsync(
            fabrica.CreateClient(), alta.Empleado.CorreoCorporativo, alta.ContrasenaTemporal);

        Assert.True(sesion.Usuario.DebeCambiarContrasena);
        Assert.Equal(alta.Empleado.Id, sesion.Usuario.EmpleadoId);
    }

    [Fact]
    public async Task AlCambiarLaContrasenaSeLevantaLaMarcaDeCambioObligatorio()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(admin, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var propio = fabrica.CreateClient();
        var sesion = await ClienteAutenticado.EntrarAsync(
            propio, alta.Empleado.CorreoCorporativo, alta.ContrasenaTemporal);
        propio.DefaultRequestHeaders.Authorization = new("Bearer", sesion.AccessToken);

        const string Nueva = "Chronos#Renovada2026";

        var cambio = await propio.PostAsJsonAsync("/api/v1/perfil/contrasena", new SolicitudCambioContrasena
        {
            ContrasenaActual = alta.ContrasenaTemporal,
            ContrasenaNueva = Nueva,
            Confirmacion = Nueva
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);

        var reingreso = await ClienteAutenticado.EntrarAsync(
            fabrica.CreateClient(), alta.Empleado.CorreoCorporativo, Nueva);

        Assert.False(reingreso.Usuario.DebeCambiarContrasena);
    }

    [Fact]
    public async Task UnaContrasenaActualEquivocadaSeSenalaEnSuPropioCampo()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(admin, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var propio = fabrica.CreateClient();
        var sesion = await ClienteAutenticado.EntrarAsync(
            propio, alta.Empleado.CorreoCorporativo, alta.ContrasenaTemporal);
        propio.DefaultRequestHeaders.Authorization = new("Bearer", sesion.AccessToken);

        var respuesta = await propio.PostAsJsonAsync("/api/v1/perfil/contrasena", new SolicitudCambioContrasena
        {
            ContrasenaActual = "EstaNoEsLaBuena#1",
            ContrasenaNueva = "Chronos#Otra2026",
            Confirmacion = "Chronos#Otra2026"
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(nameof(SolicitudCambioContrasena.ContrasenaActual), (await respuesta.ErroresAsync()).Keys);
    }

    [Fact]
    public async Task NoSePuedeRepetirElNumeroDeEmpleadoNiElCorreo()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = "EMP-0001",
            Nombres = "Duplicada",
            ApellidoPaterno = "Prueba",
            CorreoCorporativo = "duplicada@chronos.mx",
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = SembradorDatos.Ids.SedeMonterrey,
            Rol = RolChronos.Empleado
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(nameof(SolicitudCrearEmpleado.NumeroEmpleado), (await respuesta.ErroresAsync()).Keys);
    }

    [Fact]
    public async Task NoSeAdmiteUnDepartamentoQueNoPertenezcaAEsaSede()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = $"EMP-X{Interlocked.Increment(ref _consecutivo):D3}",
            Nombres = "Cruzada",
            ApellidoPaterno = "Prueba",
            CorreoCorporativo = $"cruzada{Guid.NewGuid():N}@chronos.mx",
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = SembradorDatos.Ids.SedeGuadalajara,
            Rol = RolChronos.Empleado
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("no pertenece a esa sede", (await respuesta.ErroresAsync())["sedeId"][0]);
    }

    [Fact]
    public async Task LaBajaEsLogicaYBloqueaElAcceso()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(cliente, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var baja = await cliente.DeleteAsync($"/api/v1/empleados/{alta.Empleado.Id}");
        var dado = await baja.LeerAsync<EmpleadoDto>();

        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False(dado.Activo);
        Assert.NotNull(dado.FechaBaja);

        // El expediente no se borra: las checadas históricas dependen de él.
        var recuperado = await cliente.GetAsync($"/api/v1/empleados/{alta.Empleado.Id}");
        Assert.Equal(HttpStatusCode.OK, recuperado.StatusCode);

        var intento = await fabrica.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new SolicitudLogin(alta.Empleado.CorreoCorporativo, alta.ContrasenaTemporal),
            ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);
    }

    [Fact]
    public async Task ReactivarDevuelveElAccesoYBorraLaFechaDeBaja()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(cliente, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        await cliente.DeleteAsync($"/api/v1/empleados/{alta.Empleado.Id}");
        var reactivado = await (await cliente.PostAsync($"/api/v1/empleados/{alta.Empleado.Id}/reactivar", null))
            .LeerAsync<EmpleadoDto>();

        Assert.True(reactivado.Activo);
        Assert.Null(reactivado.FechaBaja);
    }

    [Fact]
    public async Task ReiniciarElAccesoEmiteOtraTemporalYVuelveAExigirElCambio()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await CrearAsync(cliente, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var reinicio = await (await cliente.PostAsync($"/api/v1/empleados/{alta.Empleado.Id}/reiniciar-acceso", null))
            .LeerAsync<RespuestaAltaEmpleado>();

        Assert.NotEqual(alta.ContrasenaTemporal, reinicio.ContrasenaTemporal);

        var sesion = await ClienteAutenticado.EntrarAsync(
            fabrica.CreateClient(), alta.Empleado.CorreoCorporativo, reinicio.ContrasenaTemporal);

        Assert.True(sesion.Usuario.DebeCambiarContrasena);
    }

    [Fact]
    public async Task ElAdminNoPuedeDarseDeBajaASiMismo()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var sesion = await ClienteAutenticado.EntrarAsync(
            fabrica.CreateClient(), ClienteAutenticado.Admin, FabricaApiPruebas.Contrasena);

        var respuesta = await cliente.DeleteAsync($"/api/v1/empleados/{sesion.Usuario.EmpleadoId}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("a ti mismo", (await respuesta.ErroresAsync())["empleado"][0]);
    }

    [Fact]
    public async Task ElPerfilPropioIncluyeElTurnoAsignado()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var perfil = await (await cliente.GetAsync("/api/v1/perfil")).LeerAsync<MiPerfilDto>();

        Assert.Equal(ClienteAutenticado.Empleada, perfil.Correo);
        Assert.NotNull(perfil.Turno);
        Assert.Equal("Matutino", perfil.Turno.Nombre);
        Assert.Equal(8, perfil.Turno.HorasProgramadas);
    }

    internal static async Task<RespuestaAltaEmpleado> CrearAsync(
        HttpClient cliente,
        Guid departamentoId,
        Guid sedeId,
        RolChronos rol = RolChronos.Empleado)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = $"T-{sufijo}",
            Nombres = "Nuevo",
            ApellidoPaterno = "Ingreso",
            ApellidoMaterno = "Automático",
            CorreoCorporativo = $"nuevo.{sufijo}@chronos.mx",
            Puesto = "Analista en prueba",
            FechaIngreso = new DateOnly(2026, 2, 1),
            DepartamentoId = departamentoId,
            SedeId = sedeId,
            TurnoId = SembradorDatos.Ids.TurnoMatutino,
            Rol = rol
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        return await respuesta.LeerAsync<RespuestaAltaEmpleado>();
    }
}
