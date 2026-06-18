using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

public sealed record SessionSettlementInput(
    string? Notes,
    SessionEndReason EndReason,
    DateTimeOffset EndedAtUtc,
    SkillCalculationResult Skills);

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
    private const int GoldenBreakBonusPoints = 500;

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
        var skills = input.Skills;

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

        // Golden-break wins are always flagged for a manual sanity check; otherwise fall back to pace.
        var flagged = skills.HasGoldenBreakWin || IsPotentialOutlier(rawMinutes, skills.BallsPotted);

        session.Report ??= new SessionReport
        {
            SessionId = session.Id,
            BallsPotted = skills.BallsPotted,
            BallsPottedOnBreak = skills.BallsPottedOnBreak,
            GamesWon = skills.GamesWon,
            GamesLost = skills.GamesLost,
            GamesBroken = skills.GamesBroken,
            SnookersEscaped = skills.SnookersEscaped,
            SnookersFaced = skills.SnookersFaced,
            GoldenBreaks = skills.GoldenBreakWins,
            PowerDelta = skills.PowerDelta,
            AccuracyDelta = skills.AccuracyDelta,
            CueControlDelta = skills.CueControlDelta,
            SpinDelta = skills.SpinDelta,
            Notes = NormalizeNotes(input.Notes),
            FlaggedForValidation = flagged,
            SubmittedAtUtc = input.EndedAtUtc
        };

        var sessionPoints = CalculateSessionPoints(pointsMinutes);
        await pointsLedger.AwardPointsAsync(
            session.UserId,
            sessionPoints,
            PointsTransactionType.SessionCompletion,
            input.EndReason == SessionEndReason.Manual ? "Completed a pool session" : "Auto-closed pool session",
            session.Id,
            cancellationToken);

        // Flat shop-points bonus per golden-break win (AwardPointsAsync no-ops when the total is 0).
        var goldenPoints = GoldenBreakBonusPoints * skills.GoldenBreakWins;
        await pointsLedger.AwardPointsAsync(
            session.UserId,
            goldenPoints,
            PointsTransactionType.GoldenBreak,
            "Golden break bonus",
            session.Id,
            cancellationToken);

        await ApplySkillDeltasAsync(session.UserId, skills, cancellationToken);

        await AwardSessionExperienceAsync(session.UserId, skills, pointsMinutes, cancellationToken);

        await UpsertDailyMetricAsync(session.UserId, input, cancellationToken);

        return sessionPoints + goldenPoints;
    }

    private async Task AwardSessionExperienceAsync(
        Guid userId,
        SkillCalculationResult skills,
        double minutes,
        CancellationToken cancellationToken)
    {
        var experience = ExperienceCalculator.ForSession(skills, minutes);
        if (experience <= 0)
        {
            return;
        }

        var profile = await pointsLedger.GetOrCreateProfileAsync(userId, cancellationToken);
        profile.Experience += experience;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private async Task ApplySkillDeltasAsync(Guid userId, SkillCalculationResult skills, CancellationToken cancellationToken)
    {
        if (skills.PowerDelta == 0m
            && skills.AccuracyDelta == 0m
            && skills.CueControlDelta == 0m
            && skills.SpinDelta == 0m)
        {
            return;
        }

        var profile = await pointsLedger.GetOrCreateProfileAsync(userId, cancellationToken);
        profile.Power = ClampStat(profile.Power + skills.PowerDelta);
        profile.Accuracy = ClampStat(profile.Accuracy + skills.AccuracyDelta);
        profile.CueControl = ClampStat(profile.CueControl + skills.CueControlDelta);
        profile.Spin = ClampStat(profile.Spin + skills.SpinDelta);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
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

        metric.TotalBallsPotted += input.Skills.BallsPotted;
        metric.TotalGamesWon += input.Skills.GamesWon;
        metric.TotalGamesLost += input.Skills.GamesLost;
        metric.SessionsCompleted += 1;
    }

    private static decimal ClampStat(decimal value)
    {
        return Math.Clamp(value, 0m, 100m);
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
