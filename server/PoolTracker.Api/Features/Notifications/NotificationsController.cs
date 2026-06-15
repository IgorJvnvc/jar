using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Notifications.Contracts;

namespace PoolTracker.Api.Features.Notifications;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly PoolTrackerDbContext dbContext;

    public NotificationsController(PoolTrackerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpPost("register-device")]
    public async Task<ActionResult> RegisterDevice(RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var token = request.Token.Trim();
        if (token.Length == 0)
        {
            return BadRequest(new { message = "Device token is required." });
        }

        var existing = await dbContext.DeviceTokens
            .SingleOrDefaultAsync(current => current.Token == token, cancellationToken);

        if (existing is null)
        {
            dbContext.DeviceTokens.Add(new DeviceToken
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Token = token,
                Platform = request.Platform,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.UserId = userId.Value;
            existing.Platform = request.Platform;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("unregister-device")]
    public async Task<ActionResult> UnregisterDevice([FromBody] UnregisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var token = request.Token.Trim();
        if (token.Length == 0)
        {
            return BadRequest(new { message = "Device token is required." });
        }

        var existing = await dbContext.DeviceTokens
            .SingleOrDefaultAsync(current => current.UserId == userId.Value && current.Token == token, cancellationToken);

        if (existing is not null)
        {
            dbContext.DeviceTokens.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
