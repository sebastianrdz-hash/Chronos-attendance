namespace Chronos.Domain.ValueObjects;

/// <summary>
/// Perímetro circular alrededor de una sede. Sirve como verificación gruesa:
/// acota dónde ocurrió un fichaje, no prueba quién lo hizo.
/// </summary>
public sealed class Geocerca
{
    public double Latitud { get; set; }

    public double Longitud { get; set; }

    public int RadioMetros { get; set; }

    private const double RadioTierraMetros = 6_371_000d;

    public double DistanciaMetrosHasta(double latitud, double longitud)
    {
        var lat1 = double.DegreesToRadians(Latitud);
        var lat2 = double.DegreesToRadians(latitud);
        var deltaLat = double.DegreesToRadians(latitud - Latitud);
        var deltaLon = double.DegreesToRadians(longitud - Longitud);

        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);

        return RadioTierraMetros * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public bool Contiene(double latitud, double longitud) =>
        DistanciaMetrosHasta(latitud, longitud) <= RadioMetros;
}
