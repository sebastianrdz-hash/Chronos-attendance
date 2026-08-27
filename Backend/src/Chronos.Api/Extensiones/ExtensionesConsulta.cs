using System.Linq.Expressions;
using Chronos.Api.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Api.Extensiones;

public static class ExtensionesConsulta
{
    /// <summary>
    /// Cuenta y pagina en dos viajes a la base sobre el mismo IQueryable ya filtrado, de
    /// modo que el total refleje los filtros aplicados y no la tabla completa.
    /// </summary>
    public static async Task<ResultadoPaginado<TSalida>> PaginarAsync<TEntrada, TSalida>(
        this IQueryable<TEntrada> consulta,
        ConsultaNormalizada parametros,
        Expression<Func<TEntrada, TSalida>> proyeccion,
        CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .Skip(parametros.Salto)
            .Take(parametros.Tamano)
            .Select(proyeccion)
            .ToListAsync(ct);

        return new ResultadoPaginado<TSalida>(elementos, parametros.Pagina, parametros.Tamano, total);
    }

    /// <summary>
    /// Ordena por el nombre de columna que llega en la query string. El diccionario actúa
    /// como lista blanca: lo que no esté ahí cae al orden por omisión, así que un cliente
    /// no puede pedir ordenar por una columna arbitraria.
    /// </summary>
    public static IQueryable<T> OrdenarPor<T>(
        this IQueryable<T> consulta,
        string? campo,
        bool descendente,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> permitidos,
        Expression<Func<T, object?>> porOmision)
    {
        var selector = campo is not null && permitidos.TryGetValue(campo, out var encontrado)
            ? encontrado
            : porOmision;

        return descendente
            ? consulta.OrderByDescending(selector)
            : consulta.OrderBy(selector);
    }
}
