using System.Net;
using System.Net.Http.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Api.Tests;

/// <summary>
/// Comprueba que las reglas de rol se aplican en el servidor. Las pruebas unitarias del
/// dominio ya fijan la lógica; aquí se verifica que los endpoints la consultan de verdad
/// y que un cliente hostil no puede saltársela ignorando la interfaz.
/// </summary>
[Collection(nameof(ColeccionApi))]
public class AutorizacionTests(FabricaApiPruebas fabrica)
{
    // --- Empleado: solo sus propios datos ---

    [Fact]
    public async Task UnEmpleadoNoPuedeListarANadieMas()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.GetAsync("/api/v1/empleados");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Contains("su propio expediente", await respuesta.DetalleAsync());
    }

    [Fact]
    public async Task UnEmpleadoNoPuedeConsultarElExpedienteDeOtro()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var ajeno = (await (await admin.GetAsync($"/api/v1/empleados?buscar=fernando"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>()).Elementos[0];

        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);
        var respuesta = await cliente.GetAsync($"/api/v1/empleados/{ajeno.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnEmpleadoSiPuedeConsultarSuPropioExpediente()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);
        var sesion = await ClienteAutenticado.EntrarAsync(
            fabrica.CreateClient(), ClienteAutenticado.Empleada, FabricaApiPruebas.Contrasena);

        var respuesta = await cliente.GetAsync($"/api/v1/empleados/{sesion.Usuario.EmpleadoId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnEmpleadoNoPuedeLeerLosCatalogos()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        foreach (var ruta in new[] { "/api/v1/sedes", "/api/v1/departamentos", "/api/v1/turnos" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(ruta)).StatusCode);
        }
    }

    [Fact]
    public async Task UnEmpleadoNoPuedeDarDeAltaANadie()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = "EMP-INTRUSO",
            Nombres = "Intruso",
            ApellidoPaterno = "Prueba",
            CorreoCorporativo = "intruso@chronos.mx",
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = SembradorDatos.Ids.SedeMonterrey,
            Rol = RolChronos.Admin
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinTokenNoSeLlegaANingunEndpointDeGestion()
    {
        var anonimo = fabrica.CreateClient();

        foreach (var ruta in new[] { "/api/v1/empleados", "/api/v1/sedes", "/api/v1/perfil" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(ruta)).StatusCode);
        }
    }

    // --- Supervisor: lectura global, escritura acotada a su departamento ---

    [Fact]
    public async Task UnSupervisorLeeLaPlantillaCompletaAunqueNoSeaSuya()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        var pagina = await (await cliente.GetAsync("/api/v1/empleados?tamano=100"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>();

        Assert.True(pagina.Elementos.Count > 1);
        Assert.True(
            pagina.Elementos.Any(empleado => empleado.DepartamentoId != SembradorDatos.Ids.DeptoRecursosHumanos),
            "El supervisor debería ver también empleados de otros departamentos.");
    }

    [Fact]
    public async Task UnSupervisorNoPuedeEditarEmpleadosDeOtroDepartamento()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deSistemas = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoSistemas);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var respuesta = await supervisor.PutAsJsonAsync(
            $"/api/v1/empleados/{deSistemas.Id}", Edicion(deSistemas), ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Contains("su propio departamento", await respuesta.DetalleAsync());
    }

    [Fact]
    public async Task UnSupervisorSiPuedeEditarEmpleadosDeSuDepartamento()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deRh = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoRecursosHumanos);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var edicion = Edicion(deRh) with { Puesto = "Analista de nómina senior" };

        var respuesta = await supervisor.PutAsJsonAsync(
            $"/api/v1/empleados/{deRh.Id}", edicion, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Analista de nómina senior", (await respuesta.LeerAsync<EmpleadoDto>()).Puesto);
    }

    [Fact]
    public async Task UnSupervisorNoPuedeSacarAUnEmpleadoDeSuDepartamento()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deRh = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoRecursosHumanos);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var fuga = Edicion(deRh) with
        {
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = SembradorDatos.Ids.SedeMonterrey
        };

        var respuesta = await supervisor.PutAsJsonAsync(
            $"/api/v1/empleados/{deRh.Id}", fuga, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnSupervisorNoPuedeAtraerAUnEmpleadoAjenoASuDepartamento()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deSistemas = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoSistemas);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var secuestro = Edicion(deSistemas) with
        {
            DepartamentoId = SembradorDatos.Ids.DeptoRecursosHumanos,
            SedeId = SembradorDatos.Ids.SedeMonterrey
        };

        var respuesta = await supervisor.PutAsJsonAsync(
            $"/api/v1/empleados/{deSistemas.Id}", secuestro, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnSupervisorNoPuedeAscenderANadieAAdmin()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deRh = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoRecursosHumanos);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var escalada = Edicion(deRh) with { Rol = RolChronos.Admin };

        var respuesta = await supervisor.PutAsJsonAsync(
            $"/api/v1/empleados/{deRh.Id}", escalada, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Contains("administrador", await respuesta.DetalleAsync());
    }

    [Fact]
    public async Task UnSupervisorNoPuedeDarDeBajaFueraDeSuDepartamento()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deSistemas = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoSistemas);

        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var respuesta = await supervisor.DeleteAsync($"/api/v1/empleados/{deSistemas.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnSupervisorSoloDaDeAltaDentroDeSuDepartamento()
    {
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        var ajena = await supervisor.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
        {
            NumeroEmpleado = $"S-{Guid.NewGuid().ToString("N")[..8]}",
            Nombres = "Fuera",
            ApellidoPaterno = "DeAlcance",
            CorreoCorporativo = $"fuera.{Guid.NewGuid():N}@chronos.mx",
            FechaIngreso = new DateOnly(2026, 1, 1),
            DepartamentoId = SembradorDatos.Ids.DeptoSistemas,
            SedeId = SembradorDatos.Ids.SedeMonterrey,
            Rol = RolChronos.Empleado
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);

        var propia = await EmpleadosTests.CrearAsync(
            supervisor, SembradorDatos.Ids.DeptoRecursosHumanos, SembradorDatos.Ids.SedeMonterrey);

        Assert.Equal(SembradorDatos.Ids.DeptoRecursosHumanos, propia.Empleado.DepartamentoId);
    }

    [Fact]
    public async Task UnSupervisorNoPuedeCrearUnSupervisorNiUnAdmin()
    {
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        foreach (var rol in new[] { RolChronos.Supervisor, RolChronos.Admin })
        {
            var respuesta = await supervisor.PostAsJsonAsync("/api/v1/empleados", new SolicitudCrearEmpleado
            {
                NumeroEmpleado = $"S-{Guid.NewGuid().ToString("N")[..8]}",
                Nombres = "Aspirante",
                ApellidoPaterno = "Elevado",
                CorreoCorporativo = $"aspirante.{Guid.NewGuid():N}@chronos.mx",
                FechaIngreso = new DateOnly(2026, 1, 1),
                DepartamentoId = SembradorDatos.Ids.DeptoRecursosHumanos,
                SedeId = SembradorDatos.Ids.SedeMonterrey,
                Rol = rol
            }, ClienteAutenticado.Json);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task UnSupervisorNoAdministraCatalogosPeroSiLosLee()
    {
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/v1/sedes")).StatusCode);

        var creacion = await supervisor.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "Sede no autorizada",
            Codigo = "NOA-01",
            ZonaHoraria = "America/Mexico_City"
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, creacion.StatusCode);
    }

    [Fact]
    public async Task UnSupervisorAjustaSuDepartamentoPeroNoElDeOtro()
    {
        var supervisor = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        var propio = await supervisor.PutAsJsonAsync(
            $"/api/v1/departamentos/{SembradorDatos.Ids.DeptoRecursosHumanos}",
            new SolicitudDepartamento
            {
                Nombre = "Recursos Humanos",
                Codigo = "RH",
                SedeId = SembradorDatos.Ids.SedeMonterrey
            },
            ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.OK, propio.StatusCode);

        var ajeno = await supervisor.PutAsJsonAsync(
            $"/api/v1/departamentos/{SembradorDatos.Ids.DeptoSistemas}",
            new SolicitudDepartamento
            {
                Nombre = "Sistemas intervenido",
                Codigo = "TI",
                SedeId = SembradorDatos.Ids.SedeMonterrey
            },
            ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, ajeno.StatusCode);
    }

    [Fact]
    public async Task DosSupervisoresNoSePisanElAlcance()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var deOperaciones = await BuscarEnDepartamentoAsync(admin, SembradorDatos.Ids.DeptoOperaciones);

        var deRh = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);
        var deOps = await fabrica.ComoAsync(ClienteAutenticado.SupervisorOperaciones);

        var intruso = await deRh.PutAsJsonAsync(
            $"/api/v1/empleados/{deOperaciones.Id}", Edicion(deOperaciones), ClienteAutenticado.Json);
        var legitimo = await deOps.PutAsJsonAsync(
            $"/api/v1/empleados/{deOperaciones.Id}", Edicion(deOperaciones), ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Forbidden, intruso.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legitimo.StatusCode);
    }

    // --- Admin: sin fronteras ---

    [Fact]
    public async Task ElAdminMueveEmpleadosEntreDepartamentosYSedes()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await EmpleadosTests.CrearAsync(
            admin, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var traslado = Edicion(alta.Empleado) with
        {
            DepartamentoId = SembradorDatos.Ids.DeptoOperaciones,
            SedeId = SembradorDatos.Ids.SedeGuadalajara,
            TurnoId = SembradorDatos.Ids.TurnoNocturno
        };

        var respuesta = await admin.PutAsJsonAsync(
            $"/api/v1/empleados/{alta.Empleado.Id}", traslado, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var movido = await respuesta.LeerAsync<EmpleadoDto>();
        Assert.Equal("Operaciones", movido.DepartamentoNombre);
        Assert.Equal("Nocturno", movido.TurnoNombre);
    }

    [Fact]
    public async Task ElAdminSiPuedeCambiarElRolDeUnaCuenta()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var alta = await EmpleadosTests.CrearAsync(
            admin, SembradorDatos.Ids.DeptoSistemas, SembradorDatos.Ids.SedeMonterrey);

        var ascenso = Edicion(alta.Empleado) with { Rol = RolChronos.Supervisor };

        var respuesta = await admin.PutAsJsonAsync(
            $"/api/v1/empleados/{alta.Empleado.Id}", ascenso, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(RolChronos.Supervisor, (await respuesta.LeerAsync<EmpleadoDto>()).Rol);
    }

    private static async Task<EmpleadoDto> BuscarEnDepartamentoAsync(HttpClient admin, Guid departamentoId)
    {
        var pagina = await (await admin.GetAsync(
                $"/api/v1/empleados?departamentoId={departamentoId}&activo=true&tamano=100"))
            .LeerAsync<ResultadoPaginado<EmpleadoDto>>();

        return pagina.Elementos.First(empleado => empleado.Rol == RolChronos.Empleado);
    }

    private static SolicitudActualizarEmpleado Edicion(EmpleadoDto empleado) => new()
    {
        NumeroEmpleado = empleado.NumeroEmpleado,
        Nombres = empleado.Nombres,
        ApellidoPaterno = empleado.ApellidoPaterno,
        ApellidoMaterno = empleado.ApellidoMaterno,
        CorreoCorporativo = empleado.CorreoCorporativo,
        Puesto = empleado.Puesto,
        FechaIngreso = empleado.FechaIngreso,
        DepartamentoId = empleado.DepartamentoId,
        SedeId = empleado.SedeId,
        TurnoId = empleado.TurnoId,
        Rol = empleado.Rol,
        Activo = empleado.Activo
    };
}
