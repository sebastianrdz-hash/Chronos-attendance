using Chronos.Domain.Common;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Entidades;

public class Turno : EntidadBase
{
    public required string Nombre { get; set; }

    public TimeOnly HoraEntrada { get; set; }

    public TimeOnly HoraSalida { get; set; }

    /// <summary>Margen sin penalización antes de contar retardo.</summary>
    public int ToleranciaMinutos { get; set; } = 10;

    /// <summary>Minutos de comida descontados de la jornada efectiva.</summary>
    public int MinutosDescanso { get; set; } = 60;

    public DiasSemana DiasLaborales { get; set; } = DiasSemana.LunesAViernes;

    public bool Activo { get; set; } = true;

    public ICollection<Empleado> Empleados { get; set; } = [];

    /// <summary>Turnos nocturnos cuya salida cae al día siguiente.</summary>
    public bool CruzaMedianoche => HoraSalida <= HoraEntrada;

    public TimeSpan DuracionProgramada
    {
        get
        {
            var duracion = HoraSalida.ToTimeSpan() - HoraEntrada.ToTimeSpan();
            if (CruzaMedianoche)
            {
                duracion += TimeSpan.FromDays(1);
            }

            return duracion - TimeSpan.FromMinutes(MinutosDescanso);
        }
    }

    public bool EsDiaLaboral(DayOfWeek dia) => DiasLaborales.Incluye(dia);
}
