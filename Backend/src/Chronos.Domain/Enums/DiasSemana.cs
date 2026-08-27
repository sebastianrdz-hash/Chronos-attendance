namespace Chronos.Domain.Enums;

[Flags]
public enum DiasSemana
{
    Ninguno = 0,
    Domingo = 1 << 0,
    Lunes = 1 << 1,
    Martes = 1 << 2,
    Miercoles = 1 << 3,
    Jueves = 1 << 4,
    Viernes = 1 << 5,
    Sabado = 1 << 6,

    LunesAViernes = Lunes | Martes | Miercoles | Jueves | Viernes,
    LunesASabado = LunesAViernes | Sabado,
    Todos = LunesASabado | Domingo
}

public static class DiasSemanaExtensiones
{
    public static DiasSemana ADiaSemana(this DayOfWeek dia) => (DiasSemana)(1 << (int)dia);

    public static bool Incluye(this DiasSemana dias, DayOfWeek dia) => dias.HasFlag(dia.ADiaSemana());
}
