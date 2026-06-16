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

    [HttpGet("leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryResponse>>> GetLeaderboard(CancellationToken cancellationToken)
    {
        // Pull every session's report figures up front. SQLite (test provider) cannot
        // translate the grouped aggregation reliably, so we materialize and fold in memory.
        var sessionStats = await dbContext.Sessions
            .AsNoTracking()
            .Select(session => new
            {
                session.UserId,
                GamesWon = session.Report != null ? session.Report.GamesWon : 0,
                GamesLost = session.Report != null ? session.Report.GamesLost : 0,
                BallsPotted = session.Report != null ? session.Report.BallsPotted : 0
            })
            .ToListAsync(cancellationToken);

        var statsByUser = sessionStats
            .GroupBy(stat => stat.UserId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    GamesWon = group.Sum(stat => stat.GamesWon),
                    GamesLost = group.Sum(stat => stat.GamesLost),
                    BallsPotted = group.Sum(stat => stat.BallsPotted),
                    SessionCount = group.Count()
                });

        var users = await (
            from user in dbContext.Users.AsNoTracking()
            join profile in dbContext.PlayerProfiles.AsNoTracking()
                on user.Id equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            select new
            {
                user.Id,
                user.DisplayName,
                AvatarColorHex = profile != null ? profile.AvatarColorHex : "#1d7a59",
                Points = profile != null ? profile.Points : 0,
                Title = profile != null ? profile.Title : null
            })
            .ToListAsync(cancellationToken);

        var leaderboard = users
            .Select(user =>
            {
                statsByUser.TryGetValue(user.Id, out var stats);

                var gamesWon = stats?.GamesWon ?? 0;
                var gamesLost = stats?.GamesLost ?? 0;
                var gamesPlayed = gamesWon + gamesLost;
                var winRate = gamesPlayed > 0
                    ? Math.Round((decimal)gamesWon / gamesPlayed, 4)
                    : 0m;

                return new LeaderboardEntryResponse(
                    user.Id,
                    user.DisplayName,
                    user.AvatarColorHex,
                    user.Points,
                    gamesPlayed,
                    gamesWon,
                    gamesLost,
                    winRate,
                    stats?.BallsPotted ?? 0,
                    stats?.SessionCount ?? 0,
                    user.Title);
            })
            .OrderByDescending(entry => entry.WinRate)
            .ThenByDescending(entry => entry.Points)
            .ThenBy(entry => entry.DisplayName)
            .ToList();

        return Ok(leaderboard);
    }

    [HttpGet("duel-leaderboard")]
    public async Task<ActionResult<IReadOnlyList<DuelLeaderboardEntryResponse>>> GetDuelLeaderboard(CancellationToken cancellationToken)
    {
        // Join users with their profile duel tallies in memory so the win-rate ordering below
        // stays consistent across the SQLite test provider and PostgreSQL.
        var players = await (
            from user in dbContext.Users.AsNoTracking()
            join profile in dbContext.PlayerProfiles.AsNoTracking()
                on user.Id equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            select new
            {
                user.Id,
                user.DisplayName,
                AvatarColorHex = profile != null ? profile.AvatarColorHex : "#1d7a59",
                Points = profile != null ? profile.Points : 0,
                Title = profile != null ? profile.Title : null,
                DuelsWon = profile != null ? profile.DuelsWon : 0,
                DuelsLost = profile != null ? profile.DuelsLost : 0
            })
            .ToListAsync(cancellationToken);

        var duelLeaderboard = players
            .Select(player =>
            {
                var duelsPlayed = player.DuelsWon + player.DuelsLost;
                var winRate = duelsPlayed > 0
                    ? Math.Round((decimal)player.DuelsWon / duelsPlayed, 4)
                    : 0m;

                return new DuelLeaderboardEntryResponse(
                    player.Id,
                    player.DisplayName,
                    player.AvatarColorHex,
                    player.DuelsWon,
                    player.DuelsLost,
                    duelsPlayed,
                    winRate,
                    player.Points,
                    player.Title);
            })
            .Where(entry => entry.DuelsPlayed > 0)
            .OrderByDescending(entry => entry.WinRate)
            .ThenByDescending(entry => entry.DuelsWon)
            .ThenByDescending(entry => entry.Points)
            .ThenBy(entry => entry.DisplayName)
            .ToList();

        return Ok(duelLeaderboard);
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
