using System.Reflection;
using Chronos.Domain.Common;
using Chronos.Domain.Entidades;
using Chronos.Infrastructure.Identidad;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chronos.Infrastructure.Persistencia;

public class ChronosDbContext(DbContextOptions<ChronosDbContext> options)
    : IdentityDbContext<UsuarioAplicacion, RolAplicacion, Guid>(options)
{
    public DbSet<Sede> Sedes => Set<Sede>();

    public DbSet<Departamento> Departamentos => Set<Departamento>();

    public DbSet<Turno> Turnos => Set<Turno>();

    public DbSet<Empleado> Empleados => Set<Empleado>();

    public DbSet<Checada> Checadas => Set<Checada>();

    public DbSet<SenalPresencia> SenalesPresencia => Set<SenalPresencia>();

    public DbSet<CredencialWebAuthn> CredencialesWebAuthn => Set<CredencialWebAuthn>();

    public DbSet<NonceQrConsumido> NoncesQrConsumidos => Set<NonceQrConsumido>();

    public DbSet<DesafioWebAuthn> DesafiosWebAuthn => Set<DesafioWebAuthn>();

    public DbSet<AsientoBitacora> Bitacora => Set<AsientoBitacora>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entrada in ChangeTracker.Entries<EntidadBase>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entrada.Entity.ActualizadoUtc = DateTimeOffset.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
