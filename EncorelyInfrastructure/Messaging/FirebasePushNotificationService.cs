using EncorelyApplication.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace EncorelyInfrastructure.Messaging;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(ILogger<FirebasePushNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task SendPushNotificationAsync(Guid userId, string title, string body, Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        // En un entorno de producción, aquí buscaríamos el DeviceToken(s) correspondiente a userId en la BD o caché.
        // Simularemos que extraemos el token de Redis o BD y enviamos: 
        
        var message = new Message
        {
            Topic = userId.ToString(), // En este enfoque, los dispositivos pueden suscribirse a un Topic personal basado en su Guid
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data ?? new Dictionary<string, string>()
        };

        try
        {
            // var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
            _logger.LogInformation("FCM Mock: Simulated push notification sent to user {UserId}. Title: {Title}", userId, title);
            await Task.Yield();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FCM push to user {UserId}", userId);
            throw;
        }
    }
}
