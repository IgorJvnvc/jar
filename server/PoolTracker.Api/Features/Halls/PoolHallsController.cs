using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Halls.Contracts;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Features.Halls;

[ApiController]
[Authorize]
[Route("api/halls")]
public sealed class PoolHallsController : ControllerBase
{
    private const int HallAddedPoints = 15;
    private const int HallRatedPoints = 4;
    private const int TableRatedPoints = 3;

    private readonly PoolTrackerDbContext dbContext;
    private readonly IPointsLedgerService pointsLedger;
    private readonly IPoolDayEngine poolDayEngine;
    private readonly IPoolDayClock poolDayClock;

    public PoolHallsController(
        PoolTrackerDbContext dbContext,
        IPointsLedgerService pointsLedger,
        IPoolDayEngine poolDayEngine,
        IPoolDayClock poolDayClock)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
        this.poolDayEngine = poolDayEngine;
        this.poolDayClock = poolDayClock;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PoolHallResponse>>> GetHalls(CancellationToken cancellationToken)
    {
        var halls = await dbContext.PoolHalls
            .AsNoTracking()
            .Include(hall => hall.Ratings)
            .Include(hall => hall.Tables)
                .ThenInclude(table => table.Ratings)
            .ToListAsync(cancellationToken);

        var hallResponses = halls
            .Select(hall =>
            {
                var (overallScore, ratingsCount) = CalculateHallScore(hall);

                return new PoolHallResponse(
                    hall.Id,
                    hall.Name,
                    hall.Address,
                    hall.Latitude,
                    hall.Longitude,
                    hall.TotalTables,
                    overallScore,
                    ratingsCount,
                    hall.CreatedAtUtc);
            })
            .OrderByDescending(hall => hall.OverallScore)
            .ThenBy(hall => hall.Name)
            .ToList();

        return Ok(hallResponses);
    }

    [HttpGet("{hallId:guid}")]
    public async Task<ActionResult<PoolHallDetailResponse>> GetHall(Guid hallId, CancellationToken cancellationToken)
    {
        var hall = await dbContext.PoolHalls
            .AsNoTracking()
            .Where(current => current.Id == hallId)
            .Include(current => current.Ratings)
            .Include(current => current.Tables)
                .ThenInclude(table => table.Ratings)
            .SingleOrDefaultAsync(cancellationToken);

        if (hall is null)
        {
            return NotFound();
        }

        var (overallScore, ratingsCount) = CalculateHallScore(hall);

        var response = new PoolHallDetailResponse(
            hall.Id,
            hall.Name,
            hall.Address,
            hall.Latitude,
            hall.Longitude,
            hall.TotalTables,
            overallScore,
            ratingsCount,
            hall.Tables
                .OrderBy(table => table.TableLabel)
                .Select(table => new PoolHallTableResponse(
                    table.Id,
                    table.TableLabel,
                    table.Ratings.Select(rating => (decimal?)rating.OverallScore).Average() ?? 0,
                    table.Ratings.Count))
                .ToList());

        return Ok(response);
    }

    [HttpGet("competitions/recent")]
    public async Task<ActionResult<IReadOnlyList<HallDayCompetitionResponse>>> GetRecentCompetitions(CancellationToken cancellationToken)
    {
        var competitions = await dbContext.HallDayCompetitions
            .AsNoTracking()
            .Include(competition => competition.PoolHall)
            .OrderByDescending(competition => competition.PoolDate)
            .ThenByDescending(competition => competition.WinnerGamesWon)
            .Take(20)
            .ToListAsync(cancellationToken);

        var winnerIds = competitions
            .Where(competition => competition.WinnerUserId.HasValue)
            .Select(competition => competition.WinnerUserId!.Value)
            .Distinct()
            .ToList();

        var winnerNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => winnerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        var responses = competitions
            .Select(competition => new HallDayCompetitionResponse(
                competition.PoolHallId,
                competition.PoolHall.Name,
                competition.PoolDate,
                true,
                competition.WinnerUserId,
                competition.WinnerUserId is { } winnerId ? winnerNames.GetValueOrDefault(winnerId, "Player") : null,
                competition.ParticipantCount,
                competition.TotalSessions,
                competition.FinalizedAtUtc,
                Array.Empty<HallDayCompetitionEntryResponse>()))
            .ToList();

        return Ok(responses);
    }

    [HttpGet("{hallId:guid}/competition")]
    public async Task<ActionResult<HallDayCompetitionResponse>> GetHallCompetition(
        Guid hallId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var hall = await dbContext.PoolHalls
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == hallId, cancellationToken);

        if (hall is null)
        {
            return NotFound(new { message = "Pool hall not found." });
        }

        var poolDate = date ?? poolDayClock.CurrentPoolDate();

        var competition = await dbContext.HallDayCompetitions
            .AsNoTracking()
            .Include(current => current.Entries)
            .SingleOrDefaultAsync(current => current.PoolHallId == hallId && current.PoolDate == poolDate, cancellationToken);

        if (competition is not null)
        {
            var userIds = competition.Entries.Select(entry => entry.UserId).ToList();
            var displayNames = await dbContext.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

            var finalizedEntries = competition.Entries
                .OrderBy(entry => entry.Rank)
                .Select(entry => new HallDayCompetitionEntryResponse(
                    entry.UserId,
                    displayNames.GetValueOrDefault(entry.UserId, "Player"),
                    entry.Rank,
                    entry.GamesWon,
                    entry.GamesLost,
                    entry.BallsPotted,
                    entry.SessionsCompleted,
                    entry.MinutesPlayed))
                .ToList();

            return Ok(new HallDayCompetitionResponse(
                hallId,
                hall.Name,
                poolDate,
                true,
                competition.WinnerUserId,
                competition.WinnerUserId is { } winnerId ? displayNames.GetValueOrDefault(winnerId, "Player") : null,
                competition.ParticipantCount,
                competition.TotalSessions,
                competition.FinalizedAtUtc,
                finalizedEntries));
        }

        // Not finalized yet — compute the live, in-progress standings.
        var standings = await poolDayEngine.ComputeStandingsAsync(hallId, poolDate, cancellationToken);
        var liveEntries = standings
            .Select(standing => new HallDayCompetitionEntryResponse(
                standing.UserId,
                standing.DisplayName,
                standing.Rank,
                standing.GamesWon,
                standing.GamesLost,
                standing.BallsPotted,
                standing.SessionsCompleted,
                standing.MinutesPlayed))
            .ToList();

        var leader = standings.FirstOrDefault(standing => standing.GamesWon > 0);

        return Ok(new HallDayCompetitionResponse(
            hallId,
            hall.Name,
            poolDate,
            false,
            leader?.UserId,
            leader?.DisplayName,
            liveEntries.Count,
            liveEntries.Sum(entry => entry.SessionsCompleted),
            null,
            liveEntries));
    }

    [HttpPost]
    public async Task<ActionResult<PoolHallResponse>> AddHall(AddPoolHallRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var hall = new PoolHall
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TotalTables = request.TotalTables,
            AddedByUserId = userId.Value
        };

        dbContext.PoolHalls.Add(hall);
        await pointsLedger.AwardPointsAsync(
            userId.Value,
            HallAddedPoints,
            PointsTransactionType.HallAdded,
            "Added a new pool hall",
            hall.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new PoolHallResponse(
            hall.Id,
            hall.Name,
            hall.Address,
            hall.Latitude,
            hall.Longitude,
            hall.TotalTables,
            0,
            0,
            hall.CreatedAtUtc);

        return CreatedAtAction(nameof(GetHall), new { hallId = hall.Id }, response);
    }

    [HttpPost("{hallId:guid}/tables")]
    public async Task<ActionResult<PoolHallTableResponse>> AddTable(Guid hallId, AddPoolHallTableRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var hallExists = await dbContext.PoolHalls.AnyAsync(hall => hall.Id == hallId, cancellationToken);
        if (!hallExists)
        {
            return NotFound(new { message = "Pool hall not found." });
        }

        var normalizedLabel = request.TableLabel.Trim();

        var duplicateLabelExists = await dbContext.PoolHallTables.AnyAsync(
            table => table.PoolHallId == hallId && table.TableLabel == normalizedLabel,
            cancellationToken);

        if (duplicateLabelExists)
        {
            return Conflict(new { message = "This hall already contains the same table label." });
        }

        var tableEntity = new PoolHallTable
        {
            Id = Guid.NewGuid(),
            PoolHallId = hallId,
            TableLabel = normalizedLabel,
            AddedByUserId = userId.Value
        };

        dbContext.PoolHallTables.Add(tableEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetHall),
            new { hallId },
            new PoolHallTableResponse(tableEntity.Id, tableEntity.TableLabel, 0, 0));
    }

    [HttpPost("{hallId:guid}/ratings")]
    public async Task<ActionResult> RateHall(Guid hallId, RatePoolHallRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var hallExists = await dbContext.PoolHalls.AnyAsync(hall => hall.Id == hallId, cancellationToken);
        if (!hallExists)
        {
            return NotFound(new { message = "Pool hall not found." });
        }

        var utcNow = DateTimeOffset.UtcNow;
        var todayStartUtc = utcNow.UtcDateTime.Date;
        var tomorrowStartUtc = todayStartUtc.AddDays(1);
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);

        var completedSessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId.Value
                && session.PoolHallId == hallId
                && !session.IsActive
                && session.EndedAtUtc != null)
            .Select(session => session.EndedAtUtc)
            .ToListAsync(cancellationToken);

        var hasCompletedSessionToday = completedSessions.Any(current =>
        {
            if (!current.HasValue)
            {
                return false;
            }

            var endedUtc = current.Value.UtcDateTime;
            return endedUtc >= todayStartUtc && endedUtc < tomorrowStartUtc;
        });

        if (!hasCompletedSessionToday)
        {
            return BadRequest(new { message = "Hall can only be rated after completing a session there today." });
        }

        var hasRatedToday = await dbContext.PoolHallRatings.AnyAsync(
            rating => rating.PoolHallId == hallId && rating.UserId == userId.Value && rating.RatingDate == today,
            cancellationToken);

        if (hasRatedToday)
        {
            return Conflict(new { message = "You already rated this hall today." });
        }

        var overall = CalculateAverage(request.TableQuality, request.BallsQuality, request.CueQuality, request.PriceValue, request.Lighting);

        var ratingEntity = new PoolHallRating
        {
            Id = Guid.NewGuid(),
            PoolHallId = hallId,
            UserId = userId.Value,
            TableQuality = request.TableQuality,
            BallsQuality = request.BallsQuality,
            CueQuality = request.CueQuality,
            PriceValue = request.PriceValue,
            Lighting = request.Lighting,
            OverallScore = overall,
            Comment = NormalizeComment(request.Comment),
            RatingDate = today
        };

        dbContext.PoolHallRatings.Add(ratingEntity);
        await pointsLedger.AwardPointsAsync(
            userId.Value,
            HallRatedPoints,
            PointsTransactionType.HallRated,
            "Rated a pool hall",
            hallId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Rating submitted." });
    }

    [HttpPost("tables/{tableId:guid}/ratings")]
    public async Task<ActionResult> RateTable(Guid tableId, RatePoolHallTableRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var table = await dbContext.PoolHallTables
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == tableId, cancellationToken);

        if (table is null)
        {
            return NotFound(new { message = "Table not found." });
        }

        var utcNow = DateTimeOffset.UtcNow;
        var todayStartUtc = utcNow.UtcDateTime.Date;
        var tomorrowStartUtc = todayStartUtc.AddDays(1);
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);

        var completedSessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId.Value
                && session.PoolHallId == table.PoolHallId
                && session.PoolHallTableId == tableId
                && !session.IsActive
                && session.EndedAtUtc != null)
            .Select(session => session.EndedAtUtc)
            .ToListAsync(cancellationToken);

        var hasCompletedSessionToday = completedSessions.Any(current =>
        {
            if (!current.HasValue)
            {
                return false;
            }

            var endedUtc = current.Value.UtcDateTime;
            return endedUtc >= todayStartUtc && endedUtc < tomorrowStartUtc;
        });

        if (!hasCompletedSessionToday)
        {
            return BadRequest(new { message = "Table can only be rated after a completed session on that table today." });
        }

        var hasRatedToday = await dbContext.PoolHallTableRatings.AnyAsync(
            rating => rating.PoolHallTableId == tableId && rating.UserId == userId.Value && rating.RatingDate == today,
            cancellationToken);

        if (hasRatedToday)
        {
            return Conflict(new { message = "You already rated this table today." });
        }

        var overall = CalculateAverage(request.ClothQuality, request.CushionQuality, request.Levelness);

        var ratingEntity = new PoolHallTableRating
        {
            Id = Guid.NewGuid(),
            PoolHallTableId = tableId,
            UserId = userId.Value,
            ClothQuality = request.ClothQuality,
            CushionQuality = request.CushionQuality,
            Levelness = request.Levelness,
            OverallScore = overall,
            Comment = NormalizeComment(request.Comment),
            RatingDate = today
        };

        dbContext.PoolHallTableRatings.Add(ratingEntity);
        await pointsLedger.AwardPointsAsync(
            userId.Value,
            TableRatedPoints,
            PointsTransactionType.TableRated,
            "Rated a table",
            tableId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Table rating submitted." });
    }

    private static decimal CalculateAverage(params int[] values)
    {
        return Math.Round((decimal)values.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static (decimal OverallScore, int RatingsCount) CalculateHallScore(PoolHall hall)
    {
        var hallScores = hall.Ratings.Select(rating => rating.OverallScore);
        var tableScores = hall.Tables.SelectMany(table => table.Ratings).Select(rating => rating.OverallScore);
        var allScores = hallScores.Concat(tableScores).ToList();

        if (allScores.Count == 0)
        {
            return (0m, 0);
        }

        var overall = Math.Round(allScores.Average(), 2, MidpointRounding.AwayFromZero);
        return (overall, allScores.Count);
    }

    private static string? NormalizeComment(string? value)
    {
        var comment = value?.Trim();
        return string.IsNullOrWhiteSpace(comment) ? null : comment;
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
