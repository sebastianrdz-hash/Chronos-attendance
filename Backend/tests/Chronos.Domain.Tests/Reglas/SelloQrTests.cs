using System.Text;
using Chronos.Domain.Enums;
using Chronos.Domain.Reglas;

namespace Chronos.Domain.Tests.Reglas;

public class SelloQrTests
{
    private static readonly byte[] Llave = Encoding.UTF8.GetBytes("llave-de-pruebas-para-firmar-tokens-qr-32");

    private static readonly byte[] OtraLlave = Encoding.UTF8.GetBytes("una-llave-distinta-de-la-anterior-000000");

    private static readonly DateTimeOffset Ahora = new(2026, 8, 28, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnTokenRecienEmitidoSeAcepta()
    {
        var sede = Guid.CreateVersion7();
        var (_, texto) = SelloQr.Emitir(sede, Ahora, Llave);

        var lectura = SelloQr.Leer(texto, Llave, Ahora);

        Assert.True(lectura.Valido);
        Assert.Equal(MotivoRechazoQr.Ninguno, lectura.Motivo);
        Assert.Equal(sede, lectura.Token.SedeId);
    }

    [Fact]
    public void ElTokenConservaLaSedeYLaVigenciaAlIrYVolver()
    {
        var sede = Guid.CreateVersion7();
        var politica = new PoliticaQr { Vigencia = TimeSpan.FromSeconds(45) };

        var (emitido, texto) = SelloQr.Emitir(sede, Ahora, Llave, politica);
        var lectura = SelloQr.Leer(texto, Llave, Ahora, politica);

        Assert.Equal(emitido.Nonce, lectura.Token.Nonce);
        Assert.Equal(emitido.SedeId, lectura.Token.SedeId);
        Assert.Equal(Ahora.ToUnixTimeSeconds(), lectura.Token.EmitidoUtc.ToUnixTimeSeconds());
        Assert.Equal(Ahora.AddSeconds(45).ToUnixTimeSeconds(), lectura.Token.ExpiraUtc.ToUnixTimeSeconds());
    }

    [Fact]
    public void CadaEmisionLlevaUnNonceDistinto()
    {
        var sede = Guid.CreateVersion7();

        var (primero, _) = SelloQr.Emitir(sede, Ahora, Llave);
        var (segundo, _) = SelloQr.Emitir(sede, Ahora, Llave);

        Assert.NotEqual(primero.Nonce, segundo.Nonce);
    }

    [Fact]
    public void UnTokenCaducadoSeRechaza()
    {
        var politica = new PoliticaQr { Vigencia = TimeSpan.FromSeconds(30), Gracia = TimeSpan.FromSeconds(5) };
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, Llave, politica);

        var lectura = SelloQr.Leer(texto, Llave, Ahora.AddSeconds(36), politica);

        Assert.False(lectura.Valido);
        Assert.Equal(MotivoRechazoQr.Caducado, lectura.Motivo);
    }

    [Fact]
    public void LaGraciaSalvaAlQueEscaneaEnElUltimoSegundo()
    {
        var politica = new PoliticaQr { Vigencia = TimeSpan.FromSeconds(30), Gracia = TimeSpan.FromSeconds(5) };
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, Llave, politica);

        var lectura = SelloQr.Leer(texto, Llave, Ahora.AddSeconds(33), politica);

        Assert.True(lectura.Valido);
    }

    [Fact]
    public void UnTokenFirmadoConOtraLlaveSeRechaza()
    {
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, OtraLlave);

        var lectura = SelloQr.Leer(texto, Llave, Ahora);

        Assert.False(lectura.Valido);
        Assert.Equal(MotivoRechazoQr.FirmaInvalida, lectura.Motivo);
    }

    [Fact]
    public void AlterarElContenidoInvalidaLaFirma()
    {
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, Llave);

        // Se toca un carácter del payload, no de la firma: el atacante quiere cambiar la
        // sede o estirar la vigencia sin poder recalcular el HMAC.
        var alterado = texto[0] == 'A'
            ? string.Concat("B", texto.AsSpan(1))
            : string.Concat("A", texto.AsSpan(1));

        var lectura = SelloQr.Leer(alterado, Llave, Ahora);

        Assert.False(lectura.Valido);
        Assert.NotEqual(MotivoRechazoQr.Ninguno, lectura.Motivo);
    }

    [Fact]
    public void AlterarLaFirmaTambienInvalidaElToken()
    {
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, Llave);

        var alterado = texto[^1] == 'A'
            ? string.Concat(texto.AsSpan(0, texto.Length - 1), "B")
            : string.Concat(texto.AsSpan(0, texto.Length - 1), "A");

        var lectura = SelloQr.Leer(alterado, Llave, Ahora);

        Assert.False(lectura.Valido);
        Assert.Equal(MotivoRechazoQr.FirmaInvalida, lectura.Motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-token")]
    [InlineData("!!!!no-es-base64-url!!!!")]
    public void UnTextoQueNoEsTokenSeRechazaPorFormato(string? texto)
    {
        var lectura = SelloQr.Leer(texto, Llave, Ahora);

        Assert.False(lectura.Valido);
        Assert.Equal(MotivoRechazoQr.FormatoInvalido, lectura.Motivo);
    }

    [Fact]
    public void ElTextoDelTokenCabeComodoEnUnQr()
    {
        var (_, texto) = SelloQr.Emitir(Guid.CreateVersion7(), Ahora, Llave);

        Assert.Equal(SelloQr.LargoTexto, texto.Length);
    }

    [Fact]
    public void ElRefrescoSeAdelantaALaCaducidad()
    {
        var politica = new PoliticaQr { Vigencia = TimeSpan.FromSeconds(30) };

        Assert.True(politica.Refresco < politica.Vigencia);
    }
}
