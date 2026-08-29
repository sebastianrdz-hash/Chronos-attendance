using System.Net;
using System.Net.Http.Json;
using Chronos.Api.Contratos;
using Chronos.Api.Tests.Ayudantes;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Api.Tests;

[Collection(nameof(ColeccionApi))]
public class AsistenciaTests(FabricaApiPruebas fabrica)
{
    private static readonly Guid Monterrey = SembradorDatos.Ids.SedeMonterrey;
    private static readonly Guid Guadalajara = SembradorDatos.Ids.SedeGuadalajara;

    /// <summary>Domingo: ningún turno de la semilla trabaja ese día.</summary>
    private static readonly DateOnly Domingo = new(2026, 8, 30);

    // ---------- Asistencia de la plantilla ----------

    [Fact]
    public async Task QuienFichaApareceConSuEntradaYSuTurno()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("asistencia");

        await FicharAsync(sesion.Cliente, Monterrey);

        var fila = await FilaDeAsistenciaAsync(sesion.Empleado.Id);

        Assert.NotNull(fila);
        Assert.NotNull(fila.Entrada);
        Assert.Null(fila.Salida);
        Assert.Equal(TurnoContinuo, fila.TurnoNombre);

        // Entró pero no ha salido: la jornada está abierta y por eso pide revisión.
        Assert.Equal(EstadoAsistencia.JornadaIncompleta, fila.Estado);
        Assert.Equal("Jornada incompleta", fila.EstadoNombre);
    }

    [Fact]
    public async Task QuienNoFichaApareceComoFaltaSinHorasAcumuladas()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("ausente");

        var fila = await FilaDeAsistenciaAsync(sesion.Empleado.Id);

        Assert.NotNull(fila);
        Assert.Equal(EstadoAsistencia.Falta, fila.Estado);
        Assert.Null(fila.Entrada);
        Assert.Equal(0, fila.HorasTrabajadas);
    }

    [Fact]
    public async Task UnaJornadaCerradaReportaHorasYNoQuedaIncompleta()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("jornada");

        await FicharAsync(sesion.Cliente, Monterrey);
        await FicharAsync(sesion.Cliente, Monterrey, TipoChecada.Salida);

        var fila = await FilaDeAsistenciaAsync(sesion.Empleado.Id);

        Assert.NotNull(fila);
        Assert.NotNull(fila.Salida);
        Assert.NotEqual(EstadoAsistencia.JornadaIncompleta, fila.Estado);

        // Entrada y salida caen con segundos de diferencia. Lo que se comprueba es que el
        // descanso programado, mayor que lo trabajado, no produzca horas negativas ni
        // tiempo extra fantasma.
        Assert.Equal(0, fila.HorasTrabajadas);
        Assert.Equal(0, fila.HorasExtra);
    }

    [Fact]
    public async Task ElResumenNoCuentaComoFaltaAQuienEstaDeDescanso()
    {
        var asistencia = await AsistenciaAsync($"?fecha={Domingo:O}");

        var enDescanso = asistencia.Empleados.Count(e => e.Estado == EstadoAsistencia.Descanso);

        // Un domingo la plantilla esperada tiene que ser menor que la nómina completa: si
        // fueran iguales, el tablero estaría reportando como faltas los días de descanso.
        Assert.True(enDescanso > 0);
        Assert.Equal(asistencia.Empleados.Count - enDescanso, asistencia.Resumen.Plantilla);
        Assert.True(asistencia.Resumen.Faltas <= asistencia.Resumen.Plantilla);
        Assert.Equal(Domingo, asistencia.Resumen.Dia);
    }

    [Fact]
    public async Task ElFiltroPorSedeSoloDevuelveEsaSede()
    {
        var asistencia = await AsistenciaAsync($"?sedeId={Guadalajara}");

        Assert.NotEmpty(asistencia.Empleados);
        Assert.All(asistencia.Empleados, e =>
            Assert.Equal("Centro de Operaciones Guadalajara", e.SedeNombre));
    }

    [Fact]
    public async Task UnaChecadaDebilMarcaLaFilaParaRevision()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("dudosa");

        await FicharAsync(sesion.Cliente, Monterrey);

        var fila = await FilaDeAsistenciaAsync(sesion.Empleado.Id);

        Assert.NotNull(fila);
        Assert.True(fila.RequiereRevision);
        Assert.Equal(NivelConfianza.Baja, fila.ConfianzaMinima);
    }

    [Fact]
    public async Task UnaChecadaRechazadaDejaDeContarParaLaJornada()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("rechazada");

        var checada = await FicharAsync(sesion.Cliente, Monterrey);

        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        await admin.PostAsJsonAsync(
            $"/api/v1/revision/{checada.Id}/rechazar",
            new SolicitudDictamen { Motivo = "El empleado estaba incapacitado." },
            ClienteAutenticado.Json);

        var fila = await FilaDeAsistenciaAsync(sesion.Empleado.Id);

        // El dictamen tiene que verse aquí: si RH rechaza un fichaje y la nómina lo sigue
        // contando, la bandeja de revisión no sirve para nada.
        Assert.NotNull(fila);
        Assert.Equal(EstadoAsistencia.Falta, fila.Estado);
        Assert.Null(fila.Entrada);
    }

    [Fact]
    public async Task UnEmpleadoNoVeLaAsistenciaDeLaPlantilla()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);

        var respuesta = await cliente.GetAsync("/api/v1/asistencia/dia");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElSupervisorSiVeLaAsistenciaDeLaPlantilla()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.SupervisorRh);

        var respuesta = await cliente.GetAsync("/api/v1/asistencia/dia");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ---------- Jornada propia ----------

    [Fact]
    public async Task CualquierEmpleadoConsultaSuPropioHistorialDeJornadas()
    {
        var sesion = await NuevoEmpleadoDeTurnoContinuoAsync("mia");

        await FicharAsync(sesion.Cliente, Monterrey);

        var jornadas = await (await sesion.Cliente.GetAsync("/api/v1/asistencia/mia"))
            .LeerAsync<List<AsistenciaDelDiaDto>>();

        // Catorce días por omisión, incluidos los que no tienen nada: un calendario con
        // huecos explícitos se lee mejor que una lista que salta fechas.
        Assert.Equal(14, jornadas.Count);
        Assert.Contains(jornadas, j => j.Entrada is not null);
        Assert.All(jornadas, j => Assert.Equal(sesion.Empleado.Id, j.EmpleadoId));
    }

    [Fact]
    public async Task ElHistorialPropioRespetaElRangoPedido()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = hoy.AddDays(-2);

        var jornadas = await (await cliente.GetAsync($"/api/v1/asistencia/mia?desde={desde:O}&hasta={hoy:O}"))
            .LeerAsync<List<AsistenciaDelDiaDto>>();

        Assert.Equal(3, jornadas.Count);
    }

    [Fact]
    public async Task UnRangoInvertidoSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var respuesta = await cliente.GetAsync(
            $"/api/v1/asistencia/mia?desde={hoy:O}&hasta={hoy.AddDays(-5):O}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnRangoDesmedidoSeRechaza()
    {
        var cliente = await fabrica.ComoAsync(ClienteAutenticado.Empleada);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var respuesta = await cliente.GetAsync(
            $"/api/v1/asistencia/mia?desde={hoy.AddYears(-2):O}&hasta={hoy:O}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Ayudantes ----------

    private const string TurnoContinuo = "Continuo (pruebas)";

    /// <summary>
    /// Ningún turno de la semilla trabaja los siete días, así que el estado esperado de un
    /// empleado cambiaría según el día en que corra la suite. Este turno cubre la semana
    /// completa para que las pruebas den lo mismo un martes que un domingo.
    /// </summary>
    private async Task<SesionDePrueba> NuevoEmpleadoDeTurnoContinuoAsync(string prefijo) =>
        await fabrica.NuevoEmpleadoAsync(
            sedeId: Monterrey,
            turnoId: await TurnoContinuoAsync(),
            prefijo: prefijo);

    private async Task<Guid> TurnoContinuoAsync()
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);

        var existentes = await (await admin.GetAsync($"/api/v1/turnos?buscar={Uri.EscapeDataString(TurnoContinuo)}"))
            .LeerAsync<ResultadoPaginado<TurnoDto>>();

        if (existentes.Elementos.FirstOrDefault(t => t.Nombre == TurnoContinuo) is { } turno)
        {
            return turno.Id;
        }

        var creado = await admin.PostAsJsonAsync("/api/v1/turnos", new SolicitudTurno
        {
            Nombre = TurnoContinuo,
            HoraEntrada = new TimeOnly(9, 0),
            HoraSalida = new TimeOnly(18, 0),
            ToleranciaMinutos = 10,
            MinutosDescanso = 60,
            DiasLaborales = ["Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado", "Domingo"]
        }, ClienteAutenticado.Json);

        // Dos pruebas en paralelo pueden intentar crearlo a la vez; la segunda choca con el
        // nombre único y se queda con el que ya existe.
        if (!creado.IsSuccessStatusCode)
        {
            var reintento = await (await admin.GetAsync($"/api/v1/turnos?buscar={Uri.EscapeDataString(TurnoContinuo)}"))
                .LeerAsync<ResultadoPaginado<TurnoDto>>();

            return reintento.Elementos.First(t => t.Nombre == TurnoContinuo).Id;
        }

        return (await creado.LeerAsync<TurnoDto>()).Id;
    }

    private async Task<AsistenciaDto> AsistenciaAsync(string consulta = "")
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var respuesta = await admin.GetAsync($"/api/v1/asistencia/dia{consulta}");

        return await (await respuesta.AsegurarExitoAsync()).LeerAsync<AsistenciaDto>();
    }

    private async Task<AsistenciaDelDiaDto?> FilaDeAsistenciaAsync(Guid empleadoId)
    {
        var asistencia = await AsistenciaAsync();

        return asistencia.Empleados.FirstOrDefault(e => e.EmpleadoId == empleadoId);
    }

    private async Task<ChecadaDto> FicharAsync(HttpClient cliente, Guid sedeId, TipoChecada? tipo = null)
    {
        var admin = await fabrica.ComoAsync(ClienteAutenticado.Admin);
        var codigo = await (await admin.GetAsync($"/api/v1/kiosco/{sedeId}/codigo"))
            .LeerAsync<CodigoKioscoDto>();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/v1/checadas/qr",
            new SolicitudChecadaQr { Token = codigo.Token, Tipo = tipo },
            ClienteAutenticado.Json);

        return await (await respuesta.AsegurarExitoAsync()).LeerAsync<ChecadaDto>();
    }
}
