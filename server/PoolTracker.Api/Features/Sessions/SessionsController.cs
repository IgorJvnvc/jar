using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Realtime;
using PoolTracker.Api.Features.Sessions.Contracts;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Features.Sessions;

[ApiController]
[Authorize]
[Route("api/sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly PoolTrackerDbContext dbContext;
    private readonly ISessionSettlementService sessionSettlement;
    private readonly IPlayerSkillCalculator skillCalculator;
    private readonly INotificationService notificationService;

    public SessionsController(
        PoolTrackerDbContext dbContext,
        ISessionSettlementService sessionSettlement,
        IPlayerSkillCalculator skillCalculator,
        INotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.sessionSettlement = sessionSettlement;
        this.skillCalculator = skillCalculator;
        this.notificationService = notificationService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<SessionResponse>> GetActiveSession(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var session = await dbContext.Sessions
            .AsNoTracking()
            .Include(current => current.Report)
            .SingleOrDefaultAsync(current => current.UserId == userId.Value && current.IsActive, cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(session, awardedPoints: 0));
    }

    [HttpPost("start")]
    public async Task<ActionResult<SessionResponse>> StartSession(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var hallExists = await dbContext.PoolHalls.AnyAsync(hall => hall.Id == request.PoolHallId, cancellationToken);
        if (!hallExists)
        {
            return BadRequest(new { message = "Selected hall does not exist." });
        }

        if (request.PoolHallTableId.HasValue)
        {
            var tableBelongsToHall = await dbContext.PoolHallTables.AnyAsync(
                table => table.Id == request.PoolHallTableId.Value && table.PoolHallId == request.PoolHallId,
                cancellationToken);

            if (!tableBelongsToHall)
            {
                return BadRequest(new { message = "Selected table does not belong to selected hall." });
            }
        }

        var activeSession = await dbContext.Sessions
            .SingleOrDefaultAsync(current => current.UserId == userId.Value && current.IsActive, cancellationToken);

        if (activeSession is not null)
        {
            return Conflict(new { message = "You already have an active session." });
        }

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            PoolHallId = request.PoolHallId,
            PoolHallTableId = request.PoolHallTableId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.BroadcastAsync(
            "SessionStarted",
            new
            {
                sessionId = session.Id,
                userId = session.UserId,
                displayName = User.Identity?.Name ?? "Player",
                poolHallId = session.PoolHallId,
                poolHallTableId = session.PoolHallTableId,
                startedAtUtc = session.StartedAtUtc
            },
            cancellationToken);

        return CreatedAtAction(nameof(GetActiveSession), ToResponse(session, awardedPoints: 0));
    }

    [HttpPost("{sessionId:guid}/end")]
    public async Task<ActionResult<SessionResponse>> EndSession(Guid sessionId, EndSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var session = await dbContext.Sessions
            .Include(current => current.Report)
            .SingleOrDefaultAsync(current => current.Id == sessionId && current.UserId == userId.Value, cancellationToken);

        if (session is null)
        {
            return NotFound(new { message = "Session not found." });
        }

        if (!session.IsActive || session.EndedAtUtc is not null)
        {
            return Conflict(new { message = "Session is already completed." });
        }

        if (session.Report is not null)
        {
            return Conflict(new { message = "Session report already exists." });
        }

        var now = DateTimeOffset.UtcNow;

        var games = request.Games
            .Select((entry, index) => new SessionGame
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Sequence = index + 1,
                GameType = entry.GameType,
                BattleType = entry.BattleType,
                BrokeThisRack = entry.BrokeThisRack,
                BreakPots = entry.BrokeThisRack ? entry.BreakPots : 0,
                BallsPotted = entry.BallsPotted,
                SnookersFaced = entry.SnookersFaced,
                SnookersEscaped = entry.SnookersEscaped,
                Won = entry.Won,
                GoldenBreak = entry.GoldenBreak,
                PottedTrain = entry.PottedTrain,
                CreatedAtUtc = now
            })
            .ToList();

        if (games.Count > 0)
        {
            dbContext.SessionGames.AddRange(games);
        }

        var skills = skillCalculator.Calculate(games);

        var awardedPoints = await sessionSettlement.SettleAsync(
            session,
            new SessionSettlementInput(request.Notes, SessionEndReason.Manual, now, skills),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.BroadcastAsync(
            "SessionEnded",
            new
            {
                sessionId = session.Id,
                userId = session.UserId,
                displayName = User.Identity?.Name ?? "Player",
                poolHallId = session.PoolHallId,
                endedAtUtc = session.EndedAtUtc,
                awardedPoints
            },
            cancellationToken);

        return Ok(ToResponse(session, awardedPoints));
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetRecentSessions(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Include(current => current.Report)
            .Where(current => current.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        var responses = sessions
            .OrderByDescending(current => current.StartedAtUtc)
            .Take(15)
            .Select(session => ToResponse(session, awardedPoints: 0))
            .ToList();

        return Ok(responses);
    }

    private static SessionResponse ToResponse(Session session, int awardedPoints)
    {
        var report = session.Report;

        return new SessionResponse(
            session.Id,
            session.PoolHallId,
            session.PoolHallTableId,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.IsActive,
            report?.FlaggedForValidation ?? false,
            report?.BallsPotted ?? 0,
            report?.BallsPottedOnBreak ?? 0,
            report?.GamesWon ?? 0,
            report?.GamesLost ?? 0,
            report?.GamesBroken ?? 0,
            report?.SnookersFaced ?? 0,
            report?.SnookersEscaped ?? 0,
            report?.GoldenBreaks ?? 0,
            report?.PowerDelta ?? 0m,
            report?.AccuracyDelta ?? 0m,
            report?.CueControlDelta ?? 0m,
            report?.SpinDelta ?? 0m,
            awardedPoints,
            report?.Notes,
            session.EndReason);
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
