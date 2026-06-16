using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

public sealed record SessionSettlementInput(
    int BallsPotted,
    int GamesWon,
    int GamesLost,
    int SnookersEscaped,
    string? Notes,
    SessionEndReason EndReason,
    DateTimeOffset EndedAtUtc);

public interface ISessionSettlementService
{
    /// <summary>
    /// Finalizes an active session: marks it ended, writes its report, awards completion points,
    /// and rolls up the player's daily metric. Does NOT call SaveChanges — the caller owns the unit
    /// of work so multiple sessions can be settled in one batch. Returns the awarded points.
    /// </summary>
    Task<int> SettleAsync(Session session, SessionSettlementInput input, CancellationToken cancellationToken);
}

public sealed class SessionSettlementService : ISessionSettlementService
{
    private readonly PoolTrackerDbContext dbContext;
    private readonly IPointsLedgerService pointsLedger;
    private readonly IPoolDayClock poolDayClock;
    private readonly PoolDayOptions options;

    public SessionSettlementService(
        PoolTrackerDbContext dbContext,
        IPointsLedgerService pointsLedger,
        IPoolDayClock poolDayClock,
        IOptions<PoolDayOptions> options)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
        this.poolDayClock = poolDayClock;
        this.options = options.Value;
    }

    public async Task<int> SettleAsync(Session session, SessionSettlementInput input, CancellationToken cancellationToken)
    {
        session.EndedAtUtc = input.EndedAtUtc;
        session.IsActive = false;
        session.EndReason = input.EndReason;

        var rawMinutes = (input.EndedAtUtc - session.StartedAtUtc).TotalMinutes;
        if (rawMinutes < 0)
        {
            rawMinutes = 0;
        }

        // Auto-stopped sessions are capped so a forgotten/overnight session can't mint unbounded points.
        var pointsMinutes = input.EndReason == SessionEndReason.Manual
            ? rawMinutes
            : Math.Min(rawMinutes, options.IdleCapHours * 60d);

        var flagged = IsPotentialOutlier(rawMinutes, input.BallsPotted);

        session.Report ??= new SessionReport
        {
            SessionId = session.Id,
            BallsPotted = input.BallsPotted,
            GamesWon = input.GamesWon,
            GamesLost = input.GamesLost,
            SnookersEscaped = input.SnookersEscaped,
            Notes = NormalizeNotes(input.Notes),
            FlaggedForValidation = flagged,
            SubmittedAtUtc = input.EndedAtUtc
        };

        var awardedPoints = CalculateSessionPoints(pointsMinutes);
        await pointsLedger.AwardPointsAsync(
            session.UserId,
            awardedPoints,
            PointsTransactionType.SessionCompletion,
            input.EndReason == SessionEndReason.Manual ? "Completed a pool session" : "Auto-closed pool session",
            session.Id,
            cancellationToken);

        await UpsertDailyMetricAsync(session.UserId, input, cancellationToken);

        return awardedPoints;
    }

    private async Task UpsertDailyMetricAsync(Guid userId, SessionSettlementInput input, CancellationToken cancellationToken)
    {
        var date = poolDayClock.GetPoolDate(input.EndedAtUtc);
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

        metric.TotalBallsPotted += input.BallsPotted;
        metric.TotalGamesWon += input.GamesWon;
        metric.TotalGamesLost += input.GamesLost;
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
}
