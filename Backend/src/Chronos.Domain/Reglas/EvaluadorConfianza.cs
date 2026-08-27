using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;

namespace Chronos.Domain.Reglas;

public readonly record struct EvaluacionConfianza(
    int Puntaje,
    NivelConfianza Nivel,
    EstadoChecada Estado);

/// <summary>
/// Combina las señales de una checada en un solo puntaje. Regla base: cada tipo de
/// señal suma su peso una sola vez —repetir el mismo QR no vale el doble— mientras que
/// cada señal fallida o sospechosa descuenta de forma acumulativa.
/// </summary>
public static class EvaluadorConfianza
{
    public static EvaluacionConfianza Evaluar(
        IEnumerable<SenalPresencia> senales,
        PoliticaConfianza? politica = null)
    {
        politica ??= PoliticaConfianza.Predeterminada;

        var puntaje = 0;
        var tiposYaSumados = new HashSet<TipoSenal>();

        foreach (var senal in senales)
        {
            var aporte = senal.Resultado switch
            {
                ResultadoSenal.Confirmada => tiposYaSumados.Add(senal.Tipo) ? politica.PesoDe(senal.Tipo) : 0,
                ResultadoSenal.Fallida => -politica.PenalizacionFallida,
                ResultadoSenal.Sospechosa => -politica.PenalizacionSospechosa,
                _ => 0
            };

            senal.PesoAplicado = aporte;
            puntaje += aporte;
        }

        puntaje = Math.Clamp(puntaje, 0, 100);
        var nivel = politica.Clasificar(puntaje);

        return new EvaluacionConfianza(puntaje, nivel, EstadoPara(nivel));
    }

    private static EstadoChecada EstadoPara(NivelConfianza nivel) => nivel switch
    {
        NivelConfianza.Alta => EstadoChecada.Verificada,
        NivelConfianza.Nula => EstadoChecada.Rechazada,
        _ => EstadoChecada.RequiereRevision
    };
}
