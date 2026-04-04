namespace EncorelyApplication.Interfaces;

public interface IPushNotificationService
{
    Task SendPushNotificationAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default);
}
