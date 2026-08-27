using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;

namespace Chronos.Domain.Tests.Reglas;

public class PoliticaConfianzaTests
{
    [Fact]
    public void LaPoliticaPredeterminadaTraeTodosLosPesosCargados()
    {
        var politica = PoliticaConfianza.Predeterminada;

        foreach (var tipo in Enum.GetValues<TipoSenal>())
        {
            Assert.True(
                politica.PesoDe(tipo) > 0,
                $"El tipo de señal {tipo} quedó sin peso en la política predeterminada.");
        }
    }

    [Fact]
    public void LaCombinacionDeFase1AlcanzaExactamenteElUmbralAlto()
    {
        var politica = PoliticaConfianza.Predeterminada;
        var suma = politica.PesoDe(TipoSenal.CodigoQr) + politica.PesoDe(TipoSenal.WebAuthn);

        Assert.Equal(politica.UmbralAlta, suma);
    }

    [Theory]
    [InlineData(0, NivelConfianza.Nula)]
    [InlineData(25, NivelConfianza.Baja)]
    [InlineData(45, NivelConfianza.Media)]
    [InlineData(70, NivelConfianza.Alta)]
    [InlineData(100, NivelConfianza.Alta)]
    public void ClasificarUbicaElPuntajeEnSuNivel(int puntaje, NivelConfianza esperado)
    {
        Assert.Equal(esperado, PoliticaConfianza.Predeterminada.Clasificar(puntaje));
    }

    [Fact]
    public void UnaPoliticaPersonalizadaPuedeEndurecerLosUmbrales()
    {
        var estricta = new PoliticaConfianza { UmbralAlta = 90, UmbralMedia = 60 };

        Assert.Equal(NivelConfianza.Media, estricta.Clasificar(70));
        Assert.Equal(NivelConfianza.Alta, estricta.Clasificar(95));
    }
}
