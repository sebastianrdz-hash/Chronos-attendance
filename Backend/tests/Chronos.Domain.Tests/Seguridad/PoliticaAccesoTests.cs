using Chronos.Domain.Enums;
using Chronos.Domain.Seguridad;

namespace Chronos.Domain.Tests.Seguridad;

public class PoliticaAccesoTests
{
    private static readonly Guid Sistemas = new("0198f000-0000-7000-8000-0000000000a1");
    private static readonly Guid Nomina = new("0198f000-0000-7000-8000-0000000000a2");
    private static readonly Guid CarlaId = new("0198f000-0000-7000-8000-0000000000b1");
    private static readonly Guid DiegoId = new("0198f000-0000-7000-8000-0000000000b2");

    private static ContextoAcceso Admin =>
        ContextoAcceso.Para(RolChronos.Admin, empleadoId: Guid.NewGuid(), departamentoId: Sistemas);

    private static ContextoAcceso SupervisorDeSistemas =>
        ContextoAcceso.Para(RolChronos.Supervisor, empleadoId: DiegoId, departamentoId: Sistemas);

    private static ContextoAcceso EmpleadaCarla =>
        ContextoAcceso.Para(RolChronos.Empleado, empleadoId: CarlaId, departamentoId: Sistemas);

    [Fact]
    public void ElAdminPuedeListarEmpleados()
    {
        Assert.True(PoliticaAcceso.PuedeListarEmpleados(Admin).Permitido);
    }

    [Fact]
    public void ElSupervisorTieneLecturaGlobal()
    {
        Assert.True(PoliticaAcceso.PuedeListarEmpleados(SupervisorDeSistemas).Permitido);
        Assert.True(PoliticaAcceso.PuedeVerEmpleado(SupervisorDeSistemas, CarlaId).Permitido);
    }

    [Fact]
    public void ElEmpleadoNoPuedeListarANadieMas()
    {
        var decision = PoliticaAcceso.PuedeListarEmpleados(EmpleadaCarla);

        Assert.False(decision.Permitido);
        Assert.Contains("su propio expediente", decision.Motivo);
    }

    [Fact]
    public void ElEmpleadoSoloSeVeASiMismo()
    {
        Assert.True(PoliticaAcceso.PuedeVerEmpleado(EmpleadaCarla, CarlaId).Permitido);
        Assert.False(PoliticaAcceso.PuedeVerEmpleado(EmpleadaCarla, DiegoId).Permitido);
    }

    [Fact]
    public void ElSupervisorEditaDentroDeSuDepartamento()
    {
        var decision = PoliticaAcceso.PuedeEditarEmpleado(SupervisorDeSistemas, Sistemas, Sistemas);

        Assert.True(decision.Permitido);
    }

    [Fact]
    public void ElSupervisorNoEditaEmpleadosDeOtroDepartamento()
    {
        var decision = PoliticaAcceso.PuedeEditarEmpleado(SupervisorDeSistemas, Nomina, Nomina);

        Assert.False(decision.Permitido);
        Assert.Contains("su propio departamento", decision.Motivo);
    }

    [Fact]
    public void ElSupervisorNoPuedeSacarAUnEmpleadoDeSuDepartamento()
    {
        var decision = PoliticaAcceso.PuedeEditarEmpleado(SupervisorDeSistemas, Sistemas, Nomina);

        Assert.False(decision.Permitido);
    }

    [Fact]
    public void ElSupervisorNoPuedeAtraerAUnEmpleadoDeOtroDepartamento()
    {
        var decision = PoliticaAcceso.PuedeEditarEmpleado(SupervisorDeSistemas, Nomina, Sistemas);

        Assert.False(decision.Permitido);
    }

    [Fact]
    public void ElAdminMueveEmpleadosEntreDepartamentos()
    {
        Assert.True(PoliticaAcceso.PuedeEditarEmpleado(Admin, Nomina, Sistemas).Permitido);
    }

    [Fact]
    public void UnSupervisorSinDepartamentoNoEscribeNada()
    {
        var huerfano = ContextoAcceso.Para(RolChronos.Supervisor, empleadoId: DiegoId);

        var decision = PoliticaAcceso.PuedeEditarEmpleado(huerfano, Sistemas, Sistemas);

        Assert.False(decision.Permitido);
        Assert.Contains("no tiene un departamento asignado", decision.Motivo);
    }

    [Fact]
    public void ElSupervisorSoloDaDeAltaEnSuDepartamento()
    {
        Assert.True(PoliticaAcceso.PuedeCrearEmpleado(SupervisorDeSistemas, Sistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeCrearEmpleado(SupervisorDeSistemas, Nomina).Permitido);
    }

    [Fact]
    public void ElEmpleadoNoDaDeAltaANadie()
    {
        Assert.False(PoliticaAcceso.PuedeCrearEmpleado(EmpleadaCarla, Sistemas).Permitido);
    }

    [Theory]
    [InlineData(RolChronos.Supervisor)]
    [InlineData(RolChronos.Admin)]
    public void ElSupervisorNoOtorgaRolesElevados(RolChronos rolDestino)
    {
        var decision = PoliticaAcceso.PuedeAsignarRol(SupervisorDeSistemas, rolDestino);

        Assert.False(decision.Permitido);
    }

    [Fact]
    public void ElSupervisorSiPuedeAltaDeEmpleadoRaso()
    {
        Assert.True(PoliticaAcceso.PuedeAsignarRol(SupervisorDeSistemas, RolChronos.Empleado).Permitido);
    }

    [Theory]
    [InlineData(RolChronos.Empleado)]
    [InlineData(RolChronos.Supervisor)]
    [InlineData(RolChronos.Admin)]
    public void ElAdminOtorgaCualquierRol(RolChronos rolDestino)
    {
        Assert.True(PoliticaAcceso.PuedeAsignarRol(Admin, rolDestino).Permitido);
    }

    [Fact]
    public void LosCatalogosSoloSeAdministranDesdeAdmin()
    {
        Assert.True(PoliticaAcceso.PuedeAdministrarCatalogos(Admin).Permitido);
        Assert.False(PoliticaAcceso.PuedeAdministrarCatalogos(SupervisorDeSistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeAdministrarCatalogos(EmpleadaCarla).Permitido);
    }

    [Fact]
    public void ElSupervisorLeeCatalogosPeroElEmpleadoNo()
    {
        Assert.True(PoliticaAcceso.PuedeConsultarCatalogos(SupervisorDeSistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeConsultarCatalogos(EmpleadaCarla).Permitido);
    }

    [Fact]
    public void ElSupervisorAjustaSuPropioDepartamentoPeroNoElAjeno()
    {
        Assert.True(PoliticaAcceso.PuedeEditarDepartamento(SupervisorDeSistemas, Sistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeEditarDepartamento(SupervisorDeSistemas, Nomina).Permitido);
    }

    [Fact]
    public void LaBajaLogicaSigueLaMismaFronteraQueLaEdicion()
    {
        Assert.True(PoliticaAcceso.PuedeCambiarEstadoEmpleado(SupervisorDeSistemas, Sistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeCambiarEstadoEmpleado(SupervisorDeSistemas, Nomina).Permitido);
        Assert.False(PoliticaAcceso.PuedeCambiarEstadoEmpleado(EmpleadaCarla, Sistemas).Permitido);
    }

    [Fact]
    public void NadieDictaminaSuPropiaChecada()
    {
        // Ni siquiera el administrador: es la regla que sostiene todo el umbral de confianza.
        var adminSobreSiMismo = ContextoAcceso.Para(RolChronos.Admin, empleadoId: DiegoId, departamentoId: Sistemas);

        var decision = PoliticaAcceso.PuedeRevisarChecada(adminSobreSiMismo, DiegoId, Sistemas);

        Assert.False(decision.Permitido);
        Assert.Contains("su propia checada", decision.Motivo);
    }

    [Fact]
    public void ElAdminDictaminaLaChecadaDeCualquiera()
    {
        Assert.True(PoliticaAcceso.PuedeRevisarChecada(Admin, CarlaId, Nomina).Permitido);
    }

    [Fact]
    public void ElSupervisorDictaminaSoloDentroDeSuDepartamento()
    {
        Assert.True(PoliticaAcceso.PuedeRevisarChecada(SupervisorDeSistemas, CarlaId, Sistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeRevisarChecada(SupervisorDeSistemas, CarlaId, Nomina).Permitido);
    }

    [Fact]
    public void UnEmpleadoNoDictaminaChecadasAjenas()
    {
        var decision = PoliticaAcceso.PuedeRevisarChecada(EmpleadaCarla, DiegoId, Sistemas);

        Assert.False(decision.Permitido);
    }

    [Fact]
    public void LaAsistenciaAjenaEsParaAdminYSupervisor()
    {
        Assert.True(PoliticaAcceso.PuedeVerAsistencia(Admin).Permitido);
        Assert.True(PoliticaAcceso.PuedeVerAsistencia(SupervisorDeSistemas).Permitido);
        Assert.False(PoliticaAcceso.PuedeVerAsistencia(EmpleadaCarla).Permitido);
    }

    [Fact]
    public void ElRolMasAltoManda()
    {
        Assert.Equal(RolChronos.Admin, Roles_MayorPrivilegio(["Empleado", "Admin"]));
        Assert.Equal(RolChronos.Supervisor, Roles_MayorPrivilegio(["Supervisor", "Empleado"]));
        Assert.Equal(RolChronos.Empleado, Roles_MayorPrivilegio([]));

        // Réplica local de Roles.MayorPrivilegio: el dominio no referencia Infrastructure,
        // pero la regla de precedencia es de negocio y merece quedar fijada aquí.
        static RolChronos Roles_MayorPrivilegio(string[] nombres) =>
            nombres
                .Select(nombre => Enum.TryParse<RolChronos>(nombre, true, out var rol) ? rol : (RolChronos?)null)
                .Where(rol => rol is not null)
                .Select(rol => rol!.Value)
                .DefaultIfEmpty(RolChronos.Empleado)
                .Max();
    }
}
