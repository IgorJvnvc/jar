using Microsoft.AspNetCore.SignalR;

namespace PoolTracker.Api.Features.Realtime;

public interface INotificationService
{
    Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken);

    Task NotifyUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken);

    Task BroadcastAsync(string eventName, object payload, CancellationToken cancellationToken);
}

public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken)
    {
        return hubContext.Clients.User(userId.ToString()).SendCoreAsync(eventName, [payload], cancellationToken);
    }

    public Task NotifyUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken)
    {
        var ids = userIds.Select(current => current.ToString()).ToList();
        return hubContext.Clients.Users(ids).SendCoreAsync(eventName, [payload], cancellationToken);
    }

    public Task BroadcastAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendCoreAsync(eventName, [payload], cancellationToken);
    }
}
