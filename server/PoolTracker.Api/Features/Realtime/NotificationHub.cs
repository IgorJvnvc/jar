using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PoolTracker.Api.Features.Realtime;

[Authorize]
public sealed class NotificationHub : Hub
{
}
