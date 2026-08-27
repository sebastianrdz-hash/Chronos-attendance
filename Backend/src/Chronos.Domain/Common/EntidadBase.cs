namespace Chronos.Domain.Common;

public abstract class EntidadBase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ActualizadoUtc { get; set; }
}
