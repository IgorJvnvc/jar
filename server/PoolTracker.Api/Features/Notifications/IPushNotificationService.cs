namespace PoolTracker.Api.Features.Notifications;

public interface IPushNotificationService
{
    Task SendDuelChallengeAsync(
        Guid targetUserId,
        Guid duelId,
        string challengerDisplayName,
        CancellationToken cancellationToken);
}
