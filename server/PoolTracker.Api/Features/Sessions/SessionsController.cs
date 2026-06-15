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
    private readonly IPointsLedgerService pointsLedger;
    private readonly INotificationService notificationService;

    public SessionsController(
        PoolTrackerDbContext dbContext,
        IPointsLedgerService pointsLedger,
        INotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
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
        session.EndedAtUtc = now;
        session.IsActive = false;

        var durationMinutes = (session.EndedAtUtc.Value - session.StartedAtUtc).TotalMinutes;
        var flagged = IsPotentialOutlier(durationMinutes, request.BallsPotted);

        session.Report = new SessionReport
        {
            SessionId = session.Id,
            BallsPotted = request.BallsPotted,
            GamesWon = request.GamesWon,
            GamesLost = request.GamesLost,
            SnookersEscaped = request.SnookersEscaped,
            Notes = NormalizeNotes(request.Notes),
            FlaggedForValidation = flagged,
            SubmittedAtUtc = now
        };

        var awardedPoints = CalculateSessionPoints(durationMinutes);
        await pointsLedger.AwardPointsAsync(
            userId.Value,
            awardedPoints,
            PointsTransactionType.SessionCompletion,
            "Completed a pool session",
            session.Id,
            cancellationToken);

        await UpsertDailyMetricAsync(userId.Value, request, now, cancellationToken);

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

    private async Task UpsertDailyMetricAsync(Guid userId, EndSessionRequest report, DateTimeOffset endedAtUtc, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(endedAtUtc.UtcDateTime);
        var metric = await dbContext.PlayerDailyMetrics
            .SingleOrDefaultAsync(current => current.UserId == userId && current.Date == date, cancellationToken);

        if (metric is null)
        {
            metric = new PlayerDailyMetric
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = date
            };
            dbContext.PlayerDailyMetrics.Add(metric);
        }

        metric.TotalBallsPotted += report.BallsPotted;
        metric.TotalGamesWon += report.GamesWon;
        metric.TotalGamesLost += report.GamesLost;
        metric.SessionsCompleted += 1;
    }

    private static bool IsPotentialOutlier(double durationMinutes, int ballsPotted)
    {
        if (durationMinutes <= 0)
        {
            return ballsPotted > 0;
        }

        var perTenMinutes = ballsPotted / (durationMinutes / 10d);
        return perTenMinutes > 15d;
    }

    private static int CalculateSessionPoints(double durationMinutes)
    {
        var basePoints = 12;
        var durationBonus = (int)Math.Floor(durationMinutes / 20d) * 4;
        return Math.Max(basePoints + durationBonus, 5);
    }

    private static string? NormalizeNotes(string? value)
    {
        var notes = value?.Trim();
        return string.IsNullOrWhiteSpace(notes) ? null : notes;
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
            report?.GamesWon ?? 0,
            report?.GamesLost ?? 0,
            report?.SnookersEscaped ?? 0,
            awardedPoints,
            report?.Notes);
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
