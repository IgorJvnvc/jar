using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

/// <summary>One player's computed standing within a hall's pool-day competition.</summary>
public sealed record HallDayStanding(
    Guid UserId,
    string DisplayName,
    int Rank,
    int GamesWon,
    int GamesLost,
    int BallsPotted,
    int SessionsCompleted,
    int MinutesPlayed);

public interface IPoolDayEngine
{
    /// <summary>
    /// Runs the periodic pool-day maintenance: auto-stops sessions left open past the idle cap
    /// or the pool-day boundary, then finalizes the winner for every hall whose pool day has closed.
    /// Safe to call repeatedly — finalization is idempotent per (hall, pool date).
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Computes live standings for a hall on a given pool date directly from session data.
    /// Used to preview the current (not-yet-finalized) day.
    /// </summary>
    Task<IReadOnlyList<HallDayStanding>> ComputeStandingsAsync(Guid poolHallId, DateOnly poolDate, CancellationToken cancellationToken);
}

public sealed class PoolDayEngine : IPoolDayEngine
{
    // How many closed pool days to look back when finalizing, so a service outage (e.g. a weekend)
    // still gets caught up on the next run.
    private const int FinalizeLookbackDays = 7;

    private readonly PoolTrackerDbContext dbContext;
    private readonly ISessionSettlementService settlement;
    private readonly IPointsLedgerService pointsLedger;
    private readonly IPoolDayClock poolDayClock;
    private readonly TimeProvider timeProvider;
    private readonly PoolDayOptions options;
    private readonly ILogger<PoolDayEngine> logger;

    public PoolDayEngine(
        PoolTrackerDbContext dbContext,
        ISessionSettlementService settlement,
        IPointsLedgerService pointsLedger,
        IPoolDayClock poolDayClock,
        TimeProvider timeProvider,
        IOptions<PoolDayOptions> options,
        ILogger<PoolDayEngine> logger)
    {
        this.dbContext = dbContext;
        this.settlement = settlement;
        this.pointsLedger = pointsLedger;
        this.poolDayClock = poolDayClock;
        this.timeProvider = timeProvider;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Auto-stop first and persist, so sessions closed at the day boundary are visible to the
        // finalization query that follows.
        var stopped = await AutoStopStaleSessionsAsync(cancellationToken);
        if (stopped > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Pool-day sweep auto-stopped {Count} session(s).", stopped);
        }

        var finalized = await FinalizeClosedPoolDaysAsync(cancellationToken);
        if (finalized > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Pool-day sweep finalized {Count} hall competition(s).", finalized);
        }
    }

    private async Task<int> AutoStopStaleSessionsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var idleCutoff = now - TimeSpan.FromHours(options.IdleCapHours);
        var currentDayStart = poolDayClock.GetPoolDayStartUtc(poolDayClock.CurrentPoolDate());

        var activeSessions = await dbContext.Sessions
            .Include(session => session.Report)
            .Where(session => session.IsActive)
            .ToListAsync(cancellationToken);

        var stopped = 0;
        foreach (var session in activeSessions)
        {
            SessionEndReason reason;
            DateTimeOffset endedAt;

            if (session.StartedAtUtc <= idleCutoff)
            {
                reason = SessionEndReason.AutoIdle;
                endedAt = session.StartedAtUtc + TimeSpan.FromHours(options.IdleCapHours);
            }
            else if (session.StartedAtUtc < currentDayStart)
            {
                reason = SessionEndReason.AutoDayClose;
                endedAt = currentDayStart;
            }
            else
            {
                continue;
            }

            // Never settle into the future.
            if (endedAt > now)
            {
                endedAt = now;
            }

            await settlement.SettleAsync(
                session,
                new SessionSettlementInput(0, 0, 0, 0, null, reason, endedAt),
                cancellationToken);
            stopped++;
        }

        return stopped;
    }

    private async Task<int> FinalizeClosedPoolDaysAsync(CancellationToken cancellationToken)
    {
        var currentPoolDate = poolDayClock.CurrentPoolDate();
        var lookbackStart = currentPoolDate.AddDays(-FinalizeLookbackDays);
        var windowStartUtc = poolDayClock.GetPoolDayStartUtc(lookbackStart);
        var currentDayStartUtc = poolDayClock.GetPoolDayStartUtc(currentPoolDate);

        // Sessions that ended within the closed-day window, across all halls. The StartedAtUtc range
        // is filtered in memory because the SQLite provider can't translate relational DateTimeOffset
        // comparisons; the IsActive predicate still runs in SQL to bound the result set.
        var sessions = (await dbContext.Sessions
                .AsNoTracking()
                .Include(session => session.Report)
                .Include(session => session.User)
                .Where(session => !session.IsActive)
                .ToListAsync(cancellationToken))
            .Where(session => session.StartedAtUtc >= windowStartUtc
                && session.StartedAtUtc < currentDayStartUtc)
            .ToList();

        if (sessions.Count == 0)
        {
            return 0;
        }

        var alreadyFinalized = (await dbContext.HallDayCompetitions
                .AsNoTracking()
                .Where(competition => competition.PoolDate >= lookbackStart && competition.PoolDate < currentPoolDate)
                .Select(competition => new { competition.PoolHallId, competition.PoolDate })
                .ToListAsync(cancellationToken))
            .Select(item => (item.PoolHallId, item.PoolDate))
            .ToHashSet();

        // Bucket each session into its (hall, pool date) competition.
        var groups = sessions
            .GroupBy(session => (session.PoolHallId, PoolDate: poolDayClock.GetPoolDate(session.StartedAtUtc)))
            .Where(group => group.Key.PoolDate < currentPoolDate
                && !alreadyFinalized.Contains((group.Key.PoolHallId, group.Key.PoolDate)))
            .ToList();

        if (groups.Count == 0)
        {
            return 0;
        }

        var hallIds = groups.Select(group => group.Key.PoolHallId).Distinct().ToList();
        var hallNames = await dbContext.PoolHalls
            .AsNoTracking()
            .Where(hall => hallIds.Contains(hall.Id))
            .ToDictionaryAsync(hall => hall.Id, hall => hall.Name, cancellationToken);

        var finalized = 0;
        foreach (var group in groups)
        {
            var (poolHallId, poolDate) = group.Key;
            var standings = BuildStandings(group);
            var winner = standings.Count > 0 ? standings[0] : null;
            var hasWinner = winner is not null && winner.GamesWon > 0;

            var competition = new HallDayCompetition
            {
                Id = Guid.NewGuid(),
                PoolHallId = poolHallId,
                PoolDate = poolDate,
                WinnerUserId = hasWinner ? winner!.UserId : null,
                WinnerGamesWon = hasWinner ? winner!.GamesWon : 0,
                WinnerBallsPotted = hasWinner ? winner!.BallsPotted : 0,
                ParticipantCount = standings.Count,
                TotalSessions = standings.Sum(standing => standing.SessionsCompleted),
                FinalizedAtUtc = timeProvider.GetUtcNow()
            };

            foreach (var standing in standings)
            {
                competition.Entries.Add(new HallDayCompetitionEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = standing.UserId,
                    Rank = standing.Rank,
                    GamesWon = standing.GamesWon,
                    GamesLost = standing.GamesLost,
                    BallsPotted = standing.BallsPotted,
                    SessionsCompleted = standing.SessionsCompleted,
                    MinutesPlayed = standing.MinutesPlayed
                });
            }

            dbContext.HallDayCompetitions.Add(competition);

            if (hasWinner)
            {
                var hallName = hallNames.GetValueOrDefault(poolHallId, "the hall");
                await pointsLedger.AwardPointsAsync(
                    winner!.UserId,
                    options.HallWinBonusPoints,
                    PointsTransactionType.HallDayWin,
                    $"Won the day at {hallName}",
                    competition.Id,
                    cancellationToken);
            }

            finalized++;
        }

        return finalized;
    }

    public async Task<IReadOnlyList<HallDayStanding>> ComputeStandingsAsync(
        Guid poolHallId,
        DateOnly poolDate,
        CancellationToken cancellationToken)
    {
        var dayStartUtc = poolDayClock.GetPoolDayStartUtc(poolDate);
        var dayEndUtc = poolDayClock.GetPoolDayEndUtc(poolDate);

        // The StartedAtUtc range is applied in memory (SQLite can't translate relational DateTimeOffset
        // comparisons); the hall + IsActive predicates still run in SQL.
        var sessions = (await dbContext.Sessions
                .AsNoTracking()
                .Include(session => session.Report)
                .Include(session => session.User)
                .Where(session => session.PoolHallId == poolHallId && !session.IsActive)
                .ToListAsync(cancellationToken))
            .Where(session => session.StartedAtUtc >= dayStartUtc
                && session.StartedAtUtc < dayEndUtc)
            .ToList();

        return BuildStandings(sessions);
    }

    // Grouping/aggregation runs in memory (not in SQL) so the SQLite test provider — which cannot
    // translate grouped aggregates over navigations — produces identical results to Postgres.
    private static List<HallDayStanding> BuildStandings(IEnumerable<Session> sessions)
    {
        var ranked = sessions
            .GroupBy(session => session.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                DisplayName = group.Select(session => session.User?.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Player",
                GamesWon = group.Sum(session => session.Report?.GamesWon ?? 0),
                GamesLost = group.Sum(session => session.Report?.GamesLost ?? 0),
                BallsPotted = group.Sum(session => session.Report?.BallsPotted ?? 0),
                SessionsCompleted = group.Count(),
                MinutesPlayed = (int)Math.Round(group.Sum(session => session.EndedAtUtc.HasValue
                    ? Math.Max((session.EndedAtUtc.Value - session.StartedAtUtc).TotalMinutes, 0d)
                    : 0d)),
                EarliestStart = group.Min(session => session.StartedAtUtc)
            })
            .OrderByDescending(player => player.GamesWon)
            .ThenByDescending(player => player.BallsPotted)
            .ThenByDescending(player => player.MinutesPlayed)
            .ThenBy(player => player.EarliestStart)
            .ToList();

        return ranked
            .Select((player, index) => new HallDayStanding(
                player.UserId,
                player.DisplayName,
                index + 1,
                player.GamesWon,
                player.GamesLost,
                player.BallsPotted,
                player.SessionsCompleted,
                player.MinutesPlayed))
            .ToList();
    }
}
