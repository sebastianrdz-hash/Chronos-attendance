using System.Security.Claims;
using System.Text;
using Chronos.Domain.Entidades;
using Chronos.Infrastructure.Identidad;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chronos.Infrastructure.Seguridad;

public sealed record TokenEmitido(string AccessToken, DateTimeOffset ExpiraUtc, int ExpiraEnSegundos);

public interface IGeneradorTokens
{
    TokenEmitido Emitir(UsuarioAplicacion usuario, IEnumerable<string> roles, Empleado? empleado);
}

public sealed class GeneradorTokens(IOptions<OpcionesJwt> opciones) : IGeneradorTokens
{
    private readonly OpcionesJwt _opciones = opciones.Value;

    public TokenEmitido Emitir(UsuarioAplicacion usuario, IEnumerable<string> roles, Empleado? empleado)
    {
        var emitidoUtc = DateTimeOffset.UtcNow;
        var expiraUtc = emitidoUtc.AddMinutes(_opciones.MinutosVigencia);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(ClaimsChronos.NombreCompleto, usuario.NombreParaMostrar ?? usuario.UserName ?? string.Empty)
        };

        claims.AddRange(roles.Select(rol => new Claim(ClaimsChronos.Rol, rol)));

        if (empleado is not null)
        {
            claims.Add(new Claim(ClaimsChronos.EmpleadoId, empleado.Id.ToString()));
            claims.Add(new Claim(ClaimsChronos.NumeroEmpleado, empleado.NumeroEmpleado));
            claims.Add(new Claim(ClaimsChronos.SedeId, empleado.SedeId.ToString()));
        }

        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Llave));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Emisor,
            Audience = _opciones.Audiencia,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = emitidoUtc.UtcDateTime,
            NotBefore = emitidoUtc.UtcDateTime,
            Expires = expiraUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new TokenEmitido(token, expiraUtc, _opciones.MinutosVigencia * 60);
    }
}
