using System.Text.Json;
using Chronos.Domain.Entidades;
using Chronos.Domain.Enums;
using Chronos.Infrastructure.Persistencia;

namespace Chronos.Infrastructure.Auditoria;

public interface IBitacora
{
    /// <summary>
    /// Prepara un asiento y lo deja pendiente en el contexto. No guarda: quien llama
    /// decide cuándo, para que el asiento y el hecho que describe viajen en la misma
    /// transacción. Una bitácora que se confirma aparte acaba registrando cosas que no
    /// llegaron a ocurrir, o callando las que sí.
    /// </summary>
    void Registrar(
        AccionAuditada accion,
        string entidad,
        Guid? entidadId,
        Guid? usuarioId,
        string? usuarioCorreo,
        string? motivo = null,
        object? datos = null,
        string? direccionIp = null);
}

public sealed class BitacoraEnBaseDeDatos(ChronosDbContext bd, TimeProvider reloj) : IBitacora
{
    public void Registrar(
        AccionAuditada accion,
        string entidad,
        Guid? entidadId,
        Guid? usuarioId,
        string? usuarioCorreo,
        string? motivo = null,
        object? datos = null,
        string? direccionIp = null) =>
        bd.Bitacora.Add(new AsientoBitacora
        {
            OcurridoUtc = reloj.GetUtcNow(),
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            UsuarioId = usuarioId,
            UsuarioCorreo = usuarioCorreo,
            Motivo = motivo,
            DatosJson = datos is null ? null : JsonSerializer.Serialize(datos),
            DireccionIp = direccionIp
        });
}
