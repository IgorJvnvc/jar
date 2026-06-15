using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Features.Players.Contracts;

namespace PoolTracker.Api.Features.Players;

[ApiController]
[Authorize]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private readonly PoolTrackerDbContext dbContext;

    public PlayersController(PoolTrackerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet("active-sessions")]
    public async Task<ActionResult<IReadOnlyList<ActiveSessionPlayerResponse>>> GetActiveSessions(CancellationToken cancellationToken)
    {
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(current => current.IsActive)
            .Include(current => current.User)
            .Include(current => current.PoolHall)
            .Include(current => current.PoolHallTable)
            .ToListAsync(cancellationToken);

        var userIds = sessions.Select(current => current.UserId).Distinct().ToList();
        var profileMap = await dbContext.PlayerProfiles
            .AsNoTracking()
            .Where(current => userIds.Contains(current.UserId))
            .ToDictionaryAsync(current => current.UserId, cancellationToken);

        var activeSessions = sessions
            .Select(session =>
            {
                profileMap.TryGetValue(session.UserId, out var profile);

                return new ActiveSessionPlayerResponse(
                    session.UserId,
                    session.User.DisplayName,
                    profile?.AvatarColorHex ?? "#1d7a59",
                    session.Id,
                    session.PoolHallId,
                    session.PoolHall.Name,
                    session.PoolHallTableId,
                    session.PoolHallTable?.TableLabel,
                    session.StartedAtUtc);
            })
            .OrderBy(current => current.DisplayName)
            .ToList();

        return Ok(activeSessions);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlayerListItemResponse>>> GetPlayers(CancellationToken cancellationToken)
    {
        var players = await (
            from user in dbContext.Users.AsNoTracking()
            join profile in dbContext.PlayerProfiles.AsNoTracking()
                on user.Id equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            orderby user.DisplayName
            select new PlayerListItemResponse(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                profile != null ? profile.AvatarColorHex : "#1d7a59",
                profile != null ? profile.Points : 0,
                profile != null ? profile.DebtPoints : 0,
                profile != null ? profile.Title : null))
            .ToListAsync(cancellationToken);

        return Ok(players);
    }
}
