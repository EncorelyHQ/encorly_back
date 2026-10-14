namespace Encorely.Api.Features.Events;

/// <summary>Ventana en la que un evento sigue "vivo" para interactuar (radar y likes) después de empezar.</summary>
public static class EventWindow
{
    public static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    /// <summary>Un evento que empezó antes de este instante ya no admite radar ni likes.</summary>
    public static DateTimeOffset Cutoff(DateTimeOffset now) => now - Grace;
}
