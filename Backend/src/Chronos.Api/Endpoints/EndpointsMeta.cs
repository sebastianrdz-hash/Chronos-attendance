using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;

namespace Chronos.Api.Endpoints;

/// <summary>
/// Expone la política de confianza vigente. El cliente la usa para explicar en la UI
/// por qué una checada quedó marcada para revisión, sin duplicar los umbrales.
/// </summary>
public static class EndpointsMeta
{
    public sealed record DescripcionSenal(string Tipo, int Peso, string Prueba, bool DisponibleEnFase1);

    public sealed record PoliticaPublica(
        int UmbralAlta,
        int UmbralMedia,
        int PenalizacionFallida,
        int PenalizacionSospechosa,
        IReadOnlyList<DescripcionSenal> Senales);

    private static readonly Dictionary<TipoSenal, string> QueDemuestra = new()
    {
        [TipoSenal.WebAuthn] = "Que es el dispositivo del empleado y que su biometría lo desbloqueó.",
        [TipoSenal.CodigoQr] = "Que quien ficha tenía a la vista la credencial vigente.",
        [TipoSenal.BeaconBle] = "Que el dispositivo está físicamente dentro de la zona.",
        [TipoSenal.Geocerca] = "Que las coordenadas caen dentro del perímetro de la sede.",
        [TipoSenal.RedWifi] = "Que el dispositivo está conectado a la red corporativa.",
        [TipoSenal.RegistroManual] = "Que un supervisor respaldó el fichaje de forma explícita."
    };

    private static readonly HashSet<TipoSenal> Fase1 = [TipoSenal.CodigoQr, TipoSenal.WebAuthn];

    public static IEndpointRouteBuilder MapearMeta(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/v1/meta/politica-confianza", () =>
            {
                var politica = PoliticaConfianza.Predeterminada;

                var senales = Enum.GetValues<TipoSenal>()
                    .Select(tipo => new DescripcionSenal(
                        tipo.ToString(),
                        politica.PesoDe(tipo),
                        QueDemuestra[tipo],
                        Fase1.Contains(tipo)))
                    .OrderByDescending(s => s.Peso)
                    .ToList();

                return TypedResults.Ok(new PoliticaPublica(
                    politica.UmbralAlta,
                    politica.UmbralMedia,
                    politica.PenalizacionFallida,
                    politica.PenalizacionSospechosa,
                    senales));
            })
            .AllowAnonymous()
            .WithTags("Meta")
            .WithName("ObtenerPoliticaConfianza")
            .WithSummary("Pesos y umbrales con los que se califica cada checada.");

        return rutas;
    }
}
