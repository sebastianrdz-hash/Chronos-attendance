using System.Net;
using System.Net.Http.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class CatalogosTests(FabricaApiPruebas fabrica)
{
    [Fact]
    public async Task LaSemillaDejaDosSedesCuatroDepartamentosYTresTurnos()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var sedes = await (await cliente.GetAsync("/api/v1/sedes?tamano=50"))
            .LeerAsync<ResultadoPaginado<SedeDto>>();
        var departamentos = await (await cliente.GetAsync("/api/v1/departamentos?tamano=50"))
            .LeerAsync<ResultadoPaginado<DepartamentoDto>>();
        var turnos = await (await cliente.GetAsync("/api/v1/turnos?tamano=50"))
            .LeerAsync<ResultadoPaginado<TurnoDto>>();

        Assert.Equal(2, sedes.Total);
        Assert.Equal(4, departamentos.Total);
        Assert.Equal(3, turnos.Total);
    }

    [Fact]
    public async Task ExisteUnTurnoNocturnoQueCruzaMedianoche()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var turnos = await (await cliente.GetAsync("/api/v1/turnos?tamano=50"))
            .LeerAsync<ResultadoPaginado<TurnoDto>>();

        var nocturno = Assert.Single(turnos.Elementos, turno => turno.CruzaMedianoche);

        Assert.Equal("Nocturno", nocturno.Nombre);
        Assert.True(nocturno.HoraSalida < nocturno.HoraEntrada);
        Assert.Equal(7.25, nocturno.HorasProgramadas);
    }

    [Fact]
    public async Task LaPaginacionRecortaSinPerderElTotal()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var pagina = await (await cliente.GetAsync("/api/v1/departamentos?pagina=2&tamano=3"))
            .LeerAsync<ResultadoPaginado<DepartamentoDto>>();

        Assert.Equal(4, pagina.Total);
        Assert.Equal(2, pagina.Pagina);
        Assert.Single(pagina.Elementos);
        Assert.True(pagina.HayPaginaAnterior);
        Assert.False(pagina.HayPaginaSiguiente);
    }

    [Fact]
    public async Task SinParametrosSeAplicanLosValoresPorOmision()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.GetAsync("/api/v1/sedes");
        var pagina = await respuesta.LeerAsync<ResultadoPaginado<SedeDto>>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(20, pagina.Tamano);
    }

    [Fact]
    public async Task UnTamanoAbusivoSeRecortaEnLugarDeRechazarse()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var pagina = await (await cliente.GetAsync("/api/v1/sedes?tamano=99999&pagina=-4"))
            .LeerAsync<ResultadoPaginado<SedeDto>>();

        Assert.Equal(100, pagina.Tamano);
        Assert.Equal(1, pagina.Pagina);
    }

    [Fact]
    public async Task LaBusquedaFiltraPorNombreYCodigo()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var porNombre = await (await cliente.GetAsync("/api/v1/departamentos?buscar=soporte"))
            .LeerAsync<ResultadoPaginado<DepartamentoDto>>();
        var porCodigo = await (await cliente.GetAsync("/api/v1/departamentos?buscar=OPS"))
            .LeerAsync<ResultadoPaginado<DepartamentoDto>>();

        Assert.Equal("SOP", Assert.Single(porNombre.Elementos).Codigo);
        Assert.Equal("Operaciones", Assert.Single(porCodigo.Elementos).Nombre);
    }

    [Fact]
    public async Task ElOrdenDescendenteInvierteElResultado()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var ascendente = await (await cliente.GetAsync("/api/v1/turnos?ordenarPor=nombre"))
            .LeerAsync<ResultadoPaginado<TurnoDto>>();
        var descendente = await (await cliente.GetAsync("/api/v1/turnos?ordenarPor=nombre&descendente=true"))
            .LeerAsync<ResultadoPaginado<TurnoDto>>();

        Assert.Equal(
            ascendente.Elementos.Select(turno => turno.Nombre).Reverse(),
            descendente.Elementos.Select(turno => turno.Nombre));
    }

    [Fact]
    public async Task UnCampoDeOrdenDesconocidoCaeAlOrdenPorOmisionSinFallar()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.GetAsync("/api/v1/sedes?ordenarPor=' OR 1=1--");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaValidacionDevuelveLosErroresAgrupadosPorCampo()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "X",
            Codigo = "minusculas!",
            ZonaHoraria = "America/Mexico_City"
        }, ClienteAutenticado.Json);

        var errores = await respuesta.ErroresAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(nameof(SolicitudSede.Nombre), errores.Keys);
        Assert.Contains(nameof(SolicitudSede.Codigo), errores.Keys);
    }

    [Fact]
    public async Task UnaZonaHorariaInventadaSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "Sede Marciana",
            Codigo = "MAR-01",
            ZonaHoraria = "Marte/Olimpo"
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(nameof(SolicitudSede.ZonaHoraria), (await respuesta.ErroresAsync()).Keys);
    }

    [Fact]
    public async Task UnaGeocercaAMediasSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "Sede Incompleta",
            Codigo = "INC-01",
            ZonaHoraria = "America/Mexico_City",
            Latitud = 19.4,
            Longitud = null,
            RadioMetros = 100
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoSeAdmitenDosSedesConElMismoCodigo()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "Otro Monterrey",
            Codigo = "MTY-01",
            ZonaHoraria = "America/Monterrey"
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("código", (await respuesta.ErroresAsync())[nameof(SolicitudSede.Codigo)][0]);
    }

    [Fact]
    public async Task ElCicloCompletoDeUnaSedeTerminaEnBajaLogica()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var creacion = await cliente.PostAsJsonAsync("/api/v1/sedes", new SolicitudSede
        {
            Nombre = "Sede Querétaro",
            Codigo = "QRO-03",
            Direccion = "Av. 5 de Febrero 100",
            ZonaHoraria = "America/Mexico_City",
            Latitud = 20.588_1,
            Longitud = -100.389_9,
            RadioMetros = 120
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);
        var sede = await creacion.LeerAsync<SedeDto>();
        Assert.Equal(120, sede.RadioMetros);

        var edicion = await cliente.PutAsJsonAsync($"/api/v1/sedes/{sede.Id}", new SolicitudSede
        {
            Nombre = "Sede Querétaro Norte",
            Codigo = "QRO-03",
            ZonaHoraria = "America/Mexico_City"
        }, ClienteAutenticado.Json);

        var editada = await edicion.LeerAsync<SedeDto>();
        Assert.Equal("Sede Querétaro Norte", editada.Nombre);
        Assert.Null(editada.Latitud);

        var baja = await cliente.DeleteAsync($"/api/v1/sedes/{sede.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        // Baja logica: el registro sigue ahí, solo cambia de estado.
        var recuperada = await (await cliente.GetAsync($"/api/v1/sedes/{sede.Id}")).LeerAsync<SedeDto>();
        Assert.False(recuperada.Activa);
    }

    [Fact]
    public async Task UnTurnoConDescansoMasLargoQueLaJornadaSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/turnos", new SolicitudTurno
        {
            Nombre = "Imposible",
            HoraEntrada = new TimeOnly(9, 0),
            HoraSalida = new TimeOnly(12, 0),
            MinutosDescanso = 240,
            DiasLaborales = ["Lunes"]
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(nameof(SolicitudTurno.MinutosDescanso), (await respuesta.ErroresAsync()).Keys);
    }

    [Fact]
    public async Task UnDiaLaboralInventadoSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/turnos", new SolicitudTurno
        {
            Nombre = "Turno raro",
            HoraEntrada = new TimeOnly(9, 0),
            HoraSalida = new TimeOnly(17, 0),
            DiasLaborales = ["Lunes", "Octubre"]
        }, ClienteAutenticado.Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoSePuedeDarDeBajaUnDepartamentoConPlantillaActiva()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var departamentos = await (await cliente.GetAsync("/api/v1/departamentos?buscar=TI"))
            .LeerAsync<ResultadoPaginado<DepartamentoDto>>();

        var sistemas = departamentos.Elementos.First(depto => depto.Codigo == "TI");
        var respuesta = await cliente.DeleteAsync($"/api/v1/departamentos/{sistemas.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("empleados activos", (await respuesta.ErroresAsync())["departamento"][0]);
    }

    [Fact]
    public async Task UnIdentificadorInexistenteDevuelve404()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.GetAsync($"/api/v1/sedes/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnCuerpoConUnGuidMalFormadoDevuelve400YNo500()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var respuesta = await cliente.PostAsync(
            "/api/v1/departamentos",
            new StringContent(
                """{"nombre":"Prueba","codigo":"PRU","sedeId":"no-soy-un-guid"}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
