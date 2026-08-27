using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Domain.ValueObjects;
using Chronos.Infrastructure.Identidad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chronos.Infrastructure.Persistencia;

/// <summary>
/// Deja la base en un estado demostrable: dos sedes, cuatro departamentos, tres turnos
/// y una plantilla repartida entre ellos. Es idempotente, así que correrlo de nuevo no
/// duplica nada. Los identificadores son fijos para que las pruebas puedan apuntar a
/// registros concretos sin consultarlos primero.
/// </summary>
public static class SembradorDatos
{
    public static class Ids
    {
        public static readonly Guid SedeMonterrey = new("0198f000-0000-7000-8000-000000000001");
        public static readonly Guid SedeGuadalajara = new("0198f000-0000-7000-8000-000000000002");

        public static readonly Guid DeptoSistemas = new("0198f000-0000-7000-8000-000000000011");
        public static readonly Guid DeptoRecursosHumanos = new("0198f000-0000-7000-8000-000000000012");
        public static readonly Guid DeptoOperaciones = new("0198f000-0000-7000-8000-000000000013");
        public static readonly Guid DeptoSoporte = new("0198f000-0000-7000-8000-000000000014");

        public static readonly Guid TurnoMatutino = new("0198f000-0000-7000-8000-000000000021");
        public static readonly Guid TurnoNocturno = new("0198f000-0000-7000-8000-000000000022");
        public static readonly Guid TurnoVespertino = new("0198f000-0000-7000-8000-000000000023");
    }

    private sealed record PlantillaEmpleado(
        string Numero,
        string Nombres,
        string ApellidoPaterno,
        string ApellidoMaterno,
        string Correo,
        string Puesto,
        string Rol,
        Guid Departamento,
        Guid Sede,
        Guid Turno,
        DateOnly Ingreso,
        bool Activo = true);

    public static async Task SembrarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        using var alcance = servicios.CreateScope();
        var proveedor = alcance.ServiceProvider;

        var contexto = proveedor.GetRequiredService<ChronosDbContext>();
        var registro = proveedor.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SembradorDatos));
        var configuracion = proveedor.GetRequiredService<IConfiguration>();

        await contexto.Database.MigrateAsync(ct);

        await SembrarRolesAsync(proveedor, ct);
        await SembrarCatalogosAsync(contexto, ct);
        await SembrarUsuariosAsync(proveedor, contexto, configuracion, registro, ct);

        registro.LogInformation("Datos semilla verificados.");
    }

    private static async Task SembrarRolesAsync(IServiceProvider proveedor, CancellationToken ct)
    {
        var gestorRoles = proveedor.GetRequiredService<RoleManager<RolAplicacion>>();

        var descripciones = new Dictionary<string, string>
        {
            [Roles.Admin] = "Control total del sistema y la configuración.",
            [Roles.Supervisor] = "Lectura global; administra la plantilla de su departamento.",
            [Roles.Empleado] = "Registra su propia asistencia y consulta su historial."
        };

        foreach (var rol in Roles.Todos)
        {
            if (await gestorRoles.RoleExistsAsync(rol))
            {
                continue;
            }

            await gestorRoles.CreateAsync(new RolAplicacion
            {
                Name = rol,
                Descripcion = descripciones[rol]
            });
        }

        ct.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Cada registro se comprueba por su identificador en vez de preguntar si la tabla
    /// tiene algo: así una base sembrada con un catálogo anterior recibe los faltantes
    /// sin necesidad de borrarla.
    /// </summary>
    private static async Task SembrarCatalogosAsync(ChronosDbContext contexto, CancellationToken ct)
    {
        await AgregarFaltantes(
            contexto.Sedes,
            sede => sede.Id,
            ct,
                new Sede
                {
                    Id = Ids.SedeMonterrey,
                    Nombre = "Corporativo Monterrey",
                    Codigo = "MTY-01",
                    Direccion = "Av. Constitución 1000, Monterrey, Nuevo León",
                    ZonaHoraria = "America/Monterrey",
                    Geocerca = new Geocerca { Latitud = 25.669_1, Longitud = -100.309_9, RadioMetros = 150 }
                },
                new Sede
                {
                    Id = Ids.SedeGuadalajara,
                    Nombre = "Centro de Operaciones Guadalajara",
                    Codigo = "GDL-02",
                    Direccion = "Av. Vallarta 3200, Guadalajara, Jalisco",
                    ZonaHoraria = "America/Mexico_City",
                    Geocerca = new Geocerca { Latitud = 20.674_5, Longitud = -103.397_2, RadioMetros = 200 }
                });

        await AgregarFaltantes(
            contexto.Departamentos,
            depto => depto.Id,
            ct,
                new Departamento
                {
                    Id = Ids.DeptoSistemas,
                    Nombre = "Tecnologías de la Información",
                    Codigo = "TI",
                    SedeId = Ids.SedeMonterrey
                },
                new Departamento
                {
                    Id = Ids.DeptoRecursosHumanos,
                    Nombre = "Recursos Humanos",
                    Codigo = "RH",
                    SedeId = Ids.SedeMonterrey
                },
                new Departamento
                {
                    Id = Ids.DeptoOperaciones,
                    Nombre = "Operaciones",
                    Codigo = "OPS",
                    SedeId = Ids.SedeGuadalajara
                },
                new Departamento
                {
                    Id = Ids.DeptoSoporte,
                    Nombre = "Soporte a Clientes",
                    Codigo = "SOP",
                    SedeId = Ids.SedeGuadalajara
                });

        await AgregarFaltantes(
            contexto.Turnos,
            turno => turno.Id,
            ct,
                new Turno
                {
                    Id = Ids.TurnoMatutino,
                    Nombre = "Matutino",
                    HoraEntrada = new TimeOnly(9, 0),
                    HoraSalida = new TimeOnly(18, 0),
                    ToleranciaMinutos = 10,
                    MinutosDescanso = 60,
                    DiasLaborales = DiasSemana.LunesAViernes
                },
                new Turno
                {
                    Id = Ids.TurnoVespertino,
                    Nombre = "Vespertino",
                    HoraEntrada = new TimeOnly(14, 0),
                    HoraSalida = new TimeOnly(22, 0),
                    ToleranciaMinutos = 10,
                    MinutosDescanso = 45,
                    DiasLaborales = DiasSemana.LunesASabado
                },
                new Turno
                {
                    // Cruza medianoche: es el caso que ejercita el cálculo de jornada nocturna.
                    Id = Ids.TurnoNocturno,
                    Nombre = "Nocturno",
                    HoraEntrada = new TimeOnly(22, 0),
                    HoraSalida = new TimeOnly(6, 0),
                    ToleranciaMinutos = 15,
                    MinutosDescanso = 45,
                    DiasLaborales = DiasSemana.LunesASabado
                });

        await contexto.SaveChangesAsync(ct);
    }

    private static async Task AgregarFaltantes<T>(
        DbSet<T> conjunto,
        Func<T, Guid> clave,
        CancellationToken ct,
        params T[] candidatos) where T : class
    {
        var claves = candidatos.Select(clave).ToArray();
        var existentes = await conjunto
            .AsNoTracking()
            .Where(fila => claves.Contains(EF.Property<Guid>(fila, "Id")))
            .Select(fila => EF.Property<Guid>(fila, "Id"))
            .ToListAsync(ct);

        var faltantes = candidatos.Where(candidato => !existentes.Contains(clave(candidato))).ToArray();

        if (faltantes.Length > 0)
        {
            conjunto.AddRange(faltantes);
        }
    }

    private static async Task SembrarUsuariosAsync(
        IServiceProvider proveedor,
        ChronosDbContext contexto,
        IConfiguration configuracion,
        ILogger registro,
        CancellationToken ct)
    {
        var gestorUsuarios = proveedor.GetRequiredService<UserManager<UsuarioAplicacion>>();
        var contrasena = configuracion["Semilla:Contrasena"] ?? "Chronos#2026";

        foreach (var plantilla in Plantilla())
        {
            var usuario = await gestorUsuarios.FindByEmailAsync(plantilla.Correo);

            if (usuario is null)
            {
                usuario = new UsuarioAplicacion
                {
                    UserName = plantilla.Correo,
                    Email = plantilla.Correo,
                    EmailConfirmed = true,
                    NombreParaMostrar = $"{plantilla.Nombres} {plantilla.ApellidoPaterno}",
                    Activo = plantilla.Activo
                };

                var resultado = await gestorUsuarios.CreateAsync(usuario, contrasena);

                if (!resultado.Succeeded)
                {
                    registro.LogError(
                        "No se pudo crear el usuario semilla {Correo}: {Errores}",
                        plantilla.Correo,
                        string.Join("; ", resultado.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            if (!await gestorUsuarios.IsInRoleAsync(usuario, plantilla.Rol))
            {
                await gestorUsuarios.AddToRoleAsync(usuario, plantilla.Rol);
            }

            if (!await contexto.Empleados.AnyAsync(e => e.NumeroEmpleado == plantilla.Numero, ct))
            {
                contexto.Empleados.Add(new Empleado
                {
                    NumeroEmpleado = plantilla.Numero,
                    Nombres = plantilla.Nombres,
                    ApellidoPaterno = plantilla.ApellidoPaterno,
                    ApellidoMaterno = plantilla.ApellidoMaterno,
                    CorreoCorporativo = plantilla.Correo,
                    Puesto = plantilla.Puesto,
                    FechaIngreso = plantilla.Ingreso,
                    UsuarioId = usuario.Id,
                    DepartamentoId = plantilla.Departamento,
                    SedeId = plantilla.Sede,
                    TurnoId = plantilla.Turno,
                    Activo = plantilla.Activo,
                    FechaBaja = plantilla.Activo ? null : new DateOnly(2026, 3, 31)
                });
            }
        }

        await contexto.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Un supervisor por departamento y el resto de plantilla repartida. Se incluye una
    /// baja para que las listas tengan algo que filtrar con el selector de activos.
    /// </summary>
    private static PlantillaEmpleado[] Plantilla() =>
    [
        new("EMP-0001", "Ana", "Rivera", "Cantú", "admin@chronos.mx", "Directora de Sistemas",
            Roles.Admin, Ids.DeptoSistemas, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2022, 1, 10)),

        new("EMP-0002", "Bruno", "Salas", "Herrera", "supervisor@chronos.mx", "Jefe de Recursos Humanos",
            Roles.Supervisor, Ids.DeptoRecursosHumanos, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2022, 3, 1)),

        new("EMP-0003", "Carla", "Domínguez", "Vega", "empleado@chronos.mx", "Desarrolladora backend",
            Roles.Empleado, Ids.DeptoSistemas, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2023, 6, 15)),

        new("EMP-0004", "Diego", "Fuentes", "Maldonado", "diego.fuentes@chronos.mx", "Líder de Operaciones",
            Roles.Supervisor, Ids.DeptoOperaciones, Ids.SedeGuadalajara, Ids.TurnoVespertino, new DateOnly(2022, 8, 22)),

        new("EMP-0005", "Elena", "Navarro", "Ríos", "elena.navarro@chronos.mx", "Coordinadora de Soporte",
            Roles.Supervisor, Ids.DeptoSoporte, Ids.SedeGuadalajara, Ids.TurnoNocturno, new DateOnly(2023, 2, 6)),

        new("EMP-0006", "Fernando", "Ochoa", "Beltrán", "fernando.ochoa@chronos.mx", "Ingeniero de datos",
            Roles.Empleado, Ids.DeptoSistemas, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2023, 9, 4)),

        new("EMP-0007", "Gabriela", "Ponce", "Estrada", "gabriela.ponce@chronos.mx", "Analista de nómina",
            Roles.Empleado, Ids.DeptoRecursosHumanos, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2024, 1, 8)),

        new("EMP-0008", "Héctor", "Quintero", "Lozano", "hector.quintero@chronos.mx", "Reclutador",
            Roles.Empleado, Ids.DeptoRecursosHumanos, Ids.SedeMonterrey, Ids.TurnoVespertino, new DateOnly(2024, 4, 15)),

        new("EMP-0009", "Irene", "Rosales", "Márquez", "irene.rosales@chronos.mx", "Supervisora de piso",
            Roles.Empleado, Ids.DeptoOperaciones, Ids.SedeGuadalajara, Ids.TurnoVespertino, new DateOnly(2023, 11, 20)),

        new("EMP-0010", "Javier", "Trejo", "Aguilar", "javier.trejo@chronos.mx", "Operador de almacén",
            Roles.Empleado, Ids.DeptoOperaciones, Ids.SedeGuadalajara, Ids.TurnoNocturno, new DateOnly(2024, 2, 12)),

        new("EMP-0011", "Karina", "Uribe", "Solís", "karina.uribe@chronos.mx", "Operadora de almacén",
            Roles.Empleado, Ids.DeptoOperaciones, Ids.SedeGuadalajara, Ids.TurnoNocturno, new DateOnly(2024, 7, 1)),

        new("EMP-0012", "Luis", "Valdés", "Ibarra", "luis.valdes@chronos.mx", "Agente de soporte",
            Roles.Empleado, Ids.DeptoSoporte, Ids.SedeGuadalajara, Ids.TurnoNocturno, new DateOnly(2024, 5, 27)),

        new("EMP-0013", "Mariana", "Zamora", "Cortés", "mariana.zamora@chronos.mx", "Agente de soporte",
            Roles.Empleado, Ids.DeptoSoporte, Ids.SedeGuadalajara, Ids.TurnoVespertino, new DateOnly(2025, 1, 13)),

        new("EMP-0014", "Néstor", "Alarcón", "Peña", "nestor.alarcon@chronos.mx", "Especialista en QA",
            Roles.Empleado, Ids.DeptoSistemas, Ids.SedeMonterrey, Ids.TurnoMatutino, new DateOnly(2025, 3, 3)),

        new("EMP-0015", "Olivia", "Bermúdez", "Fonseca", "olivia.bermudez@chronos.mx", "Analista de soporte",
            Roles.Empleado, Ids.DeptoSoporte, Ids.SedeGuadalajara, Ids.TurnoMatutino, new DateOnly(2023, 5, 2),
            Activo: false)
    ];
}
