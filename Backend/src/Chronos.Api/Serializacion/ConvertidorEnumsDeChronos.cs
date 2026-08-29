using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronos.Api.Serializacion;

/// <summary>
/// Serializa como texto los enums propios de Chronos, y solo esos.
/// <para>
/// El motivo de que exista en vez de un JsonStringEnumConverter suelto: los convertidores
/// registrados en las opciones ganan a los que un tipo declara con atributo. Un convertidor
/// genérico de enums se apropiaba de los de Fido2, que no usan el nombre del miembro sino
/// valores con guion como <c>public-key</c>, y el navegador recibía un 400 al enrolar.
/// Acotarlo por ensamblado deja que cada biblioteca serialice los suyos como sabe.
/// </para>
/// </summary>
public sealed class ConvertidorEnumsDeChronos : JsonConverterFactory
{
    private static readonly JsonStringEnumConverter Interno = new();

    public override bool CanConvert(Type tipo) =>
        tipo.IsEnum && tipo.Assembly.GetName().Name?.StartsWith("Chronos.", StringComparison.Ordinal) == true;

    public override JsonConverter? CreateConverter(Type tipo, JsonSerializerOptions opciones) =>
        Interno.CreateConverter(tipo, opciones);
}
