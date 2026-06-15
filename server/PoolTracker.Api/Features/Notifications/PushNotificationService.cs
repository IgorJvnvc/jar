using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Features.Notifications;

public sealed class PushNotificationService : IPushNotificationService
{
    private const string AndroidDuelChannelId = "duel_challenges";
    private const string AndroidDuelSound = "duel_challenge";
    private const string IosDuelSound = "duel_challenge.caf";

    private static readonly SemaphoreSlim AppInitGate = new(1, 1);

    private readonly PoolTrackerDbContext dbContext;
    private readonly FirebaseOptions firebaseOptions;
    private readonly ILogger<PushNotificationService> logger;

    public PushNotificationService(
        PoolTrackerDbContext dbContext,
        IOptions<FirebaseOptions> firebaseOptions,
        ILogger<PushNotificationService> logger)
    {
        this.dbContext = dbContext;
        this.firebaseOptions = firebaseOptions.Value;
        this.logger = logger;
    }

    public async Task SendDuelChallengeAsync(
        Guid targetUserId,
        Guid duelId,
        string challengerDisplayName,
        CancellationToken cancellationToken)
    {
        var initialized = await EnsureFirebaseInitializedAsync(cancellationToken);
        if (!initialized)
        {
            return;
        }

        var tokens = await dbContext.DeviceTokens
            .AsNoTracking()
            .Where(deviceToken => deviceToken.UserId == targetUserId)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var deviceToken in tokens)
        {
            var message = BuildDuelChallengeMessage(deviceToken, duelId, challengerDisplayName);

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            }
            catch (FirebaseMessagingException exception) when (IsTokenInvalid(exception))
            {
                logger.LogWarning(
                    exception,
                    "Removing invalid push token for user {UserId} ({Platform}).",
                    targetUserId,
                    deviceToken.Platform);

                await RemoveTokenAsync(deviceToken.Token, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Push send failed for user {UserId} on platform {Platform}.",
                    targetUserId,
                    deviceToken.Platform);
            }
        }
    }

    private async Task<bool> EnsureFirebaseInitializedAsync(CancellationToken cancellationToken)
    {
        var credentialsPath = string.IsNullOrWhiteSpace(firebaseOptions.CredentialsPath)
            ? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_PATH")
            : firebaseOptions.CredentialsPath;

        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            logger.LogDebug("Firebase credentials path missing. Skipping push notifications.");
            return false;
        }

        if (FirebaseApp.DefaultInstance is not null)
        {
            return true;
        }

        await AppInitGate.WaitAsync(cancellationToken);
        try
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                var appOptions = new AppOptions
                {
                    Credential = GoogleCredential.FromFile(credentialsPath),
                    ProjectId = ResolveProjectId()
                };

                FirebaseApp.Create(appOptions);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize Firebase Admin SDK.");
            return false;
        }
        finally
        {
            AppInitGate.Release();
        }

        return true;
    }

    private string? ResolveProjectId()
    {
        if (!string.IsNullOrWhiteSpace(firebaseOptions.ProjectId))
        {
            return firebaseOptions.ProjectId;
        }

        var envProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        return string.IsNullOrWhiteSpace(envProjectId) ? null : envProjectId;
    }

    private static Message BuildDuelChallengeMessage(DeviceToken deviceToken, Guid duelId, string challengerDisplayName)
    {
        var title = "High Noon challenge";
        var body = $"{challengerDisplayName} challenged you to a duel.";

        var message = new Message
        {
            Token = deviceToken.Token,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = new Dictionary<string, string>
            {
                ["type"] = "duel_challenge",
                ["duelId"] = duelId.ToString(),
                ["route"] = "/duels"
            }
        };

        switch (deviceToken.Platform)
        {
            case DevicePlatform.Android:
                message.Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = AndroidDuelChannelId,
                        Sound = AndroidDuelSound
                    }
                };
                break;
            case DevicePlatform.Ios:
                message.Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = IosDuelSound
                    }
                };
                break;
        }

        return message;
    }

    private static bool IsTokenInvalid(FirebaseMessagingException exception)
    {
        return exception.MessagingErrorCode is MessagingErrorCode.InvalidArgument
            or MessagingErrorCode.Unregistered;
    }

    private async Task RemoveTokenAsync(string token, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DeviceTokens
            .SingleOrDefaultAsync(deviceToken => deviceToken.Token == token, cancellationToken);

        if (existing is null)
        {
            return;
        }

        dbContext.DeviceTokens.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
