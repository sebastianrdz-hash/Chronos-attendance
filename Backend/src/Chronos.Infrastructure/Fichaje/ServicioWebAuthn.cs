using Chronos.Domain.Entidades;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;

namespace Chronos.Infrastructure.Fichaje;

public interface IServicioWebAuthn
{
    OpcionesWebAuthn Opciones { get; }

    CredentialCreateOptions OpcionesDeEnrolamiento(
        Guid empleadoId,
        string correo,
        string nombreCompleto,
        IEnumerable<CredencialWebAuthn> yaRegistradas);

    Task<RegisteredPublicKeyCredential> VerificarEnrolamientoAsync(
        AuthenticatorAttestationRawResponse respuesta,
        CredentialCreateOptions original,
        IsCredentialIdUniqueToUserAsyncDelegate esUnica,
        CancellationToken ct);

    AssertionOptions OpcionesDeAutenticacion(IEnumerable<CredencialWebAuthn> credenciales);

    Task<VerifyAssertionResult> VerificarAutenticacionAsync(
        AuthenticatorAssertionRawResponse respuesta,
        AssertionOptions original,
        CredencialWebAuthn credencial,
        CancellationToken ct);
}

/// <summary>
/// Envoltura sobre Fido2.NET con la configuración de este servicio ya aplicada.
/// <para>
/// La biblioteca hace el trabajo criptográfico; lo que se decide aquí es la política:
/// qué se le exige al autenticador y qué se guarda después. Nada de esto toca la base de
/// datos, que queda del lado de los endpoints igual que en el fichaje por QR.
/// </para>
/// </summary>
public sealed class ServicioWebAuthn : IServicioWebAuthn
{
    private readonly Fido2 _fido2;

    public ServicioWebAuthn(IOptions<OpcionesWebAuthn> opciones)
    {
        Opciones = opciones.Value;

        _fido2 = new Fido2(new Fido2Configuration
        {
            ServerDomain = Opciones.RpId,
            ServerName = Opciones.NombreRp,
            Origins = new HashSet<string>(Opciones.OrigenesPermitidos, StringComparer.OrdinalIgnoreCase)
        });
    }

    public OpcionesWebAuthn Opciones { get; }

    public CredentialCreateOptions OpcionesDeEnrolamiento(
        Guid empleadoId,
        string correo,
        string nombreCompleto,
        IEnumerable<CredencialWebAuthn> yaRegistradas) =>
        _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                // El identificador de usuario que ve el autenticador es el del expediente,
                // no el de la cuenta de Identity: es el que sobrevive a un cambio de correo.
                Id = empleadoId.ToByteArray(),
                Name = correo,
                DisplayName = nombreCompleto
            },

            // Evita que alguien enrole dos veces el mismo dispositivo y acabe con una lista
            // de credenciales indistinguibles entre sí.
            ExcludeCredentials = [.. yaRegistradas.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))],

            AuthenticatorSelection = new AuthenticatorSelection
            {
                // Exigir verificación de usuario es lo que convierte esta señal en fuerte:
                // sin ella el autenticador solo probaría posesión del aparato, y lo que se
                // busca es que además haya habido huella, rostro o PIN.
                UserVerification = UserVerificationRequirement.Required,

                // No se piden credenciales detectables: el empleado ya viene identificado
                // por su sesión, así que no hace falta ocupar el espacio limitado de las
                // llaves de seguridad ni pedirle que elija cuenta.
                ResidentKey = ResidentKeyRequirement.Discouraged
            },

            // No se pide constancia del fabricante. Comprobarla exigiría mantener la lista
            // de metadatos de la FIDO Alliance, y para control de asistencia no aporta:
            // no interesa qué marca de sensor se usó, sino que hubo verificación.
            AttestationPreference = AttestationConveyancePreference.None
        });

    public async Task<RegisteredPublicKeyCredential> VerificarEnrolamientoAsync(
        AuthenticatorAttestationRawResponse respuesta,
        CredentialCreateOptions original,
        IsCredentialIdUniqueToUserAsyncDelegate esUnica,
        CancellationToken ct) =>
        await _fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = respuesta,
                OriginalOptions = original,
                IsCredentialIdUniqueToUserCallback = esUnica
            },
            ct);

    public AssertionOptions OpcionesDeAutenticacion(IEnumerable<CredencialWebAuthn> credenciales) =>
        _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [.. credenciales.Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))],
            UserVerification = UserVerificationRequirement.Required
        });

    public async Task<VerifyAssertionResult> VerificarAutenticacionAsync(
        AuthenticatorAssertionRawResponse respuesta,
        AssertionOptions original,
        CredencialWebAuthn credencial,
        CancellationToken ct) =>
        await _fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = respuesta,
                OriginalOptions = original,
                StoredPublicKey = credencial.ClavePublica,
                StoredSignatureCounter = (uint)credencial.ContadorFirmas,

                // El userHandle solo viene en credenciales detectables, que aquí no se
                // piden. Cuando llega, tiene que coincidir con el expediente dueño de la
                // credencial; si no llega, la comprobación no aplica.
                IsUserHandleOwnerOfCredentialIdCallback = (parametros, _) =>
                    Task.FromResult(parametros.UserHandle.SequenceEqual(credencial.IdUsuario))
            },
            ct);
}
