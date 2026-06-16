using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Duels.Contracts;
using PoolTracker.Api.Features.Notifications;
using PoolTracker.Api.Features.Realtime;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Features.Duels;

[ApiController]
[Authorize]
[Route("api/duels")]
public sealed class DuelsController : ControllerBase
{
    private const int DuelPointsWager = 100;
    private static readonly DuelStatus[] OpenStatuses =
    [
        DuelStatus.Pending,
        DuelStatus.Accepted,
        DuelStatus.AwaitingSecondReview,
        DuelStatus.CoinFlipInProgress
    ];

    private readonly PoolTrackerDbContext dbContext;
    private readonly IPointsLedgerService pointsLedger;
    private readonly INotificationService notificationService;
    private readonly IPushNotificationService pushNotificationService;
    private readonly ILogger<DuelsController> logger;

    public DuelsController(
        PoolTrackerDbContext dbContext,
        IPointsLedgerService pointsLedger,
        INotificationService notificationService,
        IPushNotificationService pushNotificationService,
        ILogger<DuelsController> logger)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
        this.notificationService = notificationService;
        this.pushNotificationService = pushNotificationService;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DuelListResponse>> List([FromQuery] DuelStatusView? status, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await ExpirePendingDuelsAsync(cancellationToken);

        var query = dbContext.Duels
            .AsNoTracking()
            .Include(duel => duel.Challenger)
            .Include(duel => duel.Opponent)
            .Include(duel => duel.ResultSubmissions)
            .Include(duel => duel.CoinFlip)
            .Where(duel => duel.ChallengerId == userId.Value || duel.OpponentId == userId.Value)
            .AsQueryable();

        if (status.HasValue)
        {
            var statusValue = ToDomain(status.Value);
            query = query.Where(duel => duel.Status == statusValue);
        }

        var duels = await query.ToListAsync(cancellationToken);
        var items = duels
            .OrderByDescending(duel => duel.CreatedAtUtc)
            .Select(duel => ToResponse(duel, userId.Value))
            .ToList();

        return Ok(new DuelListResponse(items));
    }

    [HttpGet("{duelId:guid}")]
    public async Task<ActionResult<DuelStatusResponse>> GetById(Guid duelId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var duel = await LoadDuelAsync(duelId, cancellationToken);
        if (duel is null)
        {
            return NotFound();
        }

        if (!IsParticipant(duel, userId.Value))
        {
            return Forbid();
        }

        await ExpireIfNeededAsync(duel, cancellationToken);

        return Ok(ToResponse(duel, userId.Value));
    }

    [HttpPost]
    public async Task<ActionResult<DuelStatusResponse>> Create(CreateDuelRequest request, CancellationToken cancellationToken)
    {
        var challengerId = GetCurrentUserId();
        if (challengerId is null)
        {
            return Unauthorized();
        }

        if (challengerId.Value == request.OpponentUserId)
        {
            return BadRequest(new { message = "You cannot challenge yourself." });
        }

        var opponentExists = await dbContext.Users.AnyAsync(user => user.Id == request.OpponentUserId, cancellationToken);
        if (!opponentExists)
        {
            return NotFound(new { message = "Opponent user not found." });
        }

        var hasOpenDuel = await dbContext.Duels.AnyAsync(
            duel => OpenStatuses.Contains(duel.Status)
                && ((duel.ChallengerId == challengerId.Value && duel.OpponentId == request.OpponentUserId)
                    || (duel.ChallengerId == request.OpponentUserId && duel.OpponentId == challengerId.Value)),
            cancellationToken);

        if (hasOpenDuel)
        {
            return Conflict(new { message = "There is already an open duel between these players." });
        }

        var now = DateTimeOffset.UtcNow;

        var duel = new Duel
        {
            Id = Guid.NewGuid(),
            ChallengerId = challengerId.Value,
            OpponentId = request.OpponentUserId,
            Status = DuelStatus.Pending,
            PointsWager = DuelPointsWager,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24)
        };

        dbContext.Duels.Add(duel);
        await dbContext.SaveChangesAsync(cancellationToken);

        duel = await LoadDuelAsync(duel.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created duel could not be loaded.");

        await notificationService.NotifyUserAsync(
            duel.OpponentId,
            "DuelChallengeReceived",
            ToResponse(duel, duel.OpponentId),
            cancellationToken);

        try
        {
            await pushNotificationService.SendDuelChallengeAsync(
                duel.OpponentId,
                duel.Id,
                duel.Challenger.DisplayName,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Push notification dispatch failed for duel {DuelId}.", duel.Id);
        }

        return CreatedAtAction(nameof(GetById), new { duelId = duel.Id }, ToResponse(duel, challengerId.Value));
    }

    [HttpPost("{duelId:guid}/respond")]
    public async Task<ActionResult<DuelStatusResponse>> Respond(Guid duelId, RespondDuelRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var duel = await LoadDuelAsync(duelId, cancellationToken);
        if (duel is null)
        {
            return NotFound();
        }

        if (duel.OpponentId != userId.Value)
        {
            return Forbid();
        }

        await ExpireIfNeededAsync(duel, cancellationToken);

        if (duel.Status != DuelStatus.Pending)
        {
            return Conflict(new { message = "Duel is no longer awaiting response." });
        }

        duel.RespondedAtUtc = DateTimeOffset.UtcNow;
        duel.Status = request.Accept ? DuelStatus.Accepted : DuelStatus.Declined;

        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(
            duel.ChallengerId,
            "DuelResponseReceived",
            ToResponse(duel, duel.ChallengerId),
            cancellationToken);

        return Ok(ToResponse(duel, userId.Value));
    }

    [HttpPost("{duelId:guid}/submit-result")]
    public async Task<ActionResult<DuelStatusResponse>> SubmitResult(Guid duelId, SubmitDuelResultRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var duel = await LoadDuelAsync(duelId, cancellationToken);
        if (duel is null)
        {
            return NotFound();
        }

        if (!IsParticipant(duel, userId.Value))
        {
            return Forbid();
        }

        await ExpireIfNeededAsync(duel, cancellationToken);

        if (duel.Status is not DuelStatus.Accepted and not DuelStatus.AwaitingSecondReview)
        {
            return Conflict(new { message = "Duel is not accepting results right now." });
        }

        var roundNumber = duel.Status == DuelStatus.AwaitingSecondReview ? 2 : 1;
        var alreadySubmittedRound = duel.ResultSubmissions.Any(
            submission => submission.SubmittedByUserId == userId.Value && submission.RoundNumber == roundNumber);

        if (alreadySubmittedRound)
        {
            return Conflict(new { message = "You already submitted your result for this round." });
        }

        var submission = new DuelResultSubmission
        {
            Id = Guid.NewGuid(),
            DuelId = duel.Id,
            SubmittedByUserId = userId.Value,
            RoundNumber = roundNumber,
            Choice = ToDomain(request.Choice),
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.DuelResultSubmissions.Add(submission);

        var roundSubmissions = duel.ResultSubmissions
            .Where(current => current.RoundNumber == roundNumber)
            .OrderBy(current => current.SubmittedAtUtc)
            .ToList();

        if (roundSubmissions.Count < 2)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var opponentId = GetOpponentId(duel, userId.Value);
            await notificationService.NotifyUserAsync(
                opponentId,
                "DuelResultPending",
                ToResponse(duel, opponentId),
                cancellationToken);

            return Ok(ToResponse(duel, userId.Value));
        }

        var first = roundSubmissions[0];
        var second = roundSubmissions[1];
        var firstClaimedWinner = ResolveClaimedWinner(duel, first);
        var secondClaimedWinner = ResolveClaimedWinner(duel, second);

        if (firstClaimedWinner == secondClaimedWinner)
        {
            var winnerUserId = firstClaimedWinner;

            await CompleteDuelAsync(duel, winnerUserId, cancellationToken);

            await NotifyParticipantsAsync(duel, "DuelCompleted", cancellationToken);

            return Ok(ToResponse(duel, userId.Value));
        }

        if (roundNumber == 1)
        {
            duel.Status = DuelStatus.AwaitingSecondReview;

            await dbContext.SaveChangesAsync(cancellationToken);

            await NotifyParticipantsAsync(duel, "DuelResultPending", cancellationToken);

            return Ok(ToResponse(duel, userId.Value));
        }

        await StartCoinFlipAsync(duel, cancellationToken);

        await NotifyParticipantsAsync(duel, "DuelResultPending", cancellationToken);

        return Ok(ToResponse(duel, userId.Value));
    }

    [HttpPost("{duelId:guid}/coin-flip/choose")]
    public async Task<ActionResult<DuelStatusResponse>> ChooseCoinSide(Guid duelId, ChooseCoinSideRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var duel = await LoadDuelAsync(duelId, cancellationToken);
        if (duel is null)
        {
            return NotFound();
        }

        if (!IsParticipant(duel, userId.Value))
        {
            return Forbid();
        }

        if (duel.Status != DuelStatus.CoinFlipInProgress || duel.CoinFlip is null)
        {
            return Conflict(new { message = "Coin flip is not active for this duel." });
        }

        if (duel.CoinFlip.ResultSide.HasValue)
        {
            return Conflict(new { message = "Coin flip already resolved." });
        }

        var selected = ToDomain(request.Side);

        if (userId.Value == duel.CoinFlip.FirstChooserUserId)
        {
            if (duel.CoinFlip.FirstChooserSide.HasValue)
            {
                return Conflict(new { message = "You already chose a side." });
            }

            duel.CoinFlip.FirstChooserSide = selected;
            duel.CoinFlip.SecondChooserUserId = GetOpponentId(duel, userId.Value);

            await dbContext.SaveChangesAsync(cancellationToken);

            await notificationService.NotifyUserAsync(
                duel.CoinFlip.SecondChooserUserId.Value,
                "DuelResultPending",
                ToResponse(duel, duel.CoinFlip.SecondChooserUserId.Value),
                cancellationToken);

            return Ok(ToResponse(duel, userId.Value));
        }

        if (!duel.CoinFlip.FirstChooserSide.HasValue)
        {
            return Conflict(new { message = "Second chooser cannot pick before the first chooser." });
        }

        if (duel.CoinFlip.SecondChooserUserId != userId.Value)
        {
            return Forbid();
        }

        if (duel.CoinFlip.SecondChooserSide.HasValue)
        {
            return Conflict(new { message = "You already chose a side." });
        }

        if (selected == duel.CoinFlip.FirstChooserSide.Value)
        {
            return Conflict(new { message = "Coin side already taken by the first chooser." });
        }

        duel.CoinFlip.SecondChooserSide = selected;

        var random = Random.Shared.Next(0, 2) == 0 ? CoinSide.Heads : CoinSide.Tails;
        duel.CoinFlip.ResultSide = random;
        duel.CoinFlip.ResolvedAtUtc = DateTimeOffset.UtcNow;

        duel.CoinFlip.WinnerUserId = duel.CoinFlip.FirstChooserSide == random
            ? duel.CoinFlip.FirstChooserUserId
            : duel.CoinFlip.SecondChooserUserId;

        await CompleteDuelAsync(duel, duel.CoinFlip.WinnerUserId.Value, cancellationToken);

        await NotifyParticipantsAsync(duel, "DuelCompleted", cancellationToken);

        return Ok(ToResponse(duel, userId.Value));
    }

    private async Task<Duel?> LoadDuelAsync(Guid duelId, CancellationToken cancellationToken)
    {
        return await dbContext.Duels
            .Include(duel => duel.Challenger)
            .Include(duel => duel.Opponent)
            .Include(duel => duel.ResultSubmissions)
            .Include(duel => duel.CoinFlip)
            .SingleOrDefaultAsync(duel => duel.Id == duelId, cancellationToken);
    }

    private async Task CompleteDuelAsync(Duel duel, Guid winnerUserId, CancellationToken cancellationToken)
    {
        var loserUserId = duel.ChallengerId == winnerUserId ? duel.OpponentId : duel.ChallengerId;

        duel.Status = DuelStatus.Completed;
        duel.WinnerUserId = winnerUserId;
        duel.CompletedAtUtc = DateTimeOffset.UtcNow;

        await pointsLedger.AwardPointsAsync(
            winnerUserId,
            duel.PointsWager,
            PointsTransactionType.DuelWin,
            "Won a duel",
            duel.Id,
            cancellationToken);

        await pointsLedger.DeductPointsAllowDebtAsync(
            loserUserId,
            duel.PointsWager,
            PointsTransactionType.DuelLoss,
            "Lost a duel",
            duel.Id,
            cancellationToken);

        var winnerProfile = await pointsLedger.GetOrCreateProfileAsync(winnerUserId, cancellationToken);
        winnerProfile.DuelsWon++;

        var loserProfile = await pointsLedger.GetOrCreateProfileAsync(loserUserId, cancellationToken);
        loserProfile.DuelsLost++;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyParticipantsAsync(Duel duel, string eventName, CancellationToken cancellationToken)
    {
        await notificationService.NotifyUserAsync(
            duel.ChallengerId,
            eventName,
            ToResponse(duel, duel.ChallengerId),
            cancellationToken);

        await notificationService.NotifyUserAsync(
            duel.OpponentId,
            eventName,
            ToResponse(duel, duel.OpponentId),
            cancellationToken);
    }

    private static Guid ResolveClaimedWinner(Duel duel, DuelResultSubmission submission)
    {
        if (submission.Choice == DuelPlayerResultChoice.Won)
        {
            return submission.SubmittedByUserId;
        }

        return GetOpponentId(duel, submission.SubmittedByUserId);
    }

    private async Task StartCoinFlipAsync(Duel duel, CancellationToken cancellationToken)
    {
        duel.Status = DuelStatus.CoinFlipInProgress;

        if (duel.CoinFlip is null)
        {
            duel.CoinFlip = new DuelCoinFlip
            {
                Id = Guid.NewGuid(),
                DuelId = duel.Id,
                FirstChooserUserId = duel.ChallengerId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.DuelCoinFlips.Add(duel.CoinFlip);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpirePendingDuelsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pendingIds = await dbContext.Duels
            .AsNoTracking()
            .Where(duel => duel.Status == DuelStatus.Pending)
            .Select(duel => new { duel.Id, duel.ExpiresAtUtc })
            .ToListAsync(cancellationToken);

        var stale = pendingIds
            .Where(duel => duel.ExpiresAtUtc < now)
            .Select(duel => duel.Id)
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        var toExpire = await dbContext.Duels
            .Where(duel => stale.Contains(duel.Id))
            .ToListAsync(cancellationToken);

        foreach (var duel in toExpire)
        {
            duel.Status = DuelStatus.Expired;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireIfNeededAsync(Duel duel, CancellationToken cancellationToken)
    {
        if (duel.Status != DuelStatus.Pending)
        {
            return;
        }

        var expiresAtUtc = duel.ExpiresAtUtc.UtcDateTime;
        if (expiresAtUtc >= DateTime.UtcNow)
        {
            return;
        }

        duel.Status = DuelStatus.Expired;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsParticipant(Duel duel, Guid userId)
    {
        return duel.ChallengerId == userId || duel.OpponentId == userId;
    }

    private static Guid GetOpponentId(Duel duel, Guid currentUserId)
    {
        return duel.ChallengerId == currentUserId ? duel.OpponentId : duel.ChallengerId;
    }

    private static DuelStatusResponse ToResponse(Duel duel, Guid viewerUserId)
    {
        var currentRound = duel.Status switch
        {
            DuelStatus.AwaitingSecondReview or DuelStatus.CoinFlipInProgress => 2,
            _ => 1
        };

        var mySubmission = duel.ResultSubmissions
            .Where(current => current.SubmittedByUserId == viewerUserId)
            .OrderByDescending(current => current.RoundNumber)
            .ThenByDescending(current => current.SubmittedAtUtc)
            .FirstOrDefault();

        var opponentSubmission = duel.ResultSubmissions
            .Where(current => current.SubmittedByUserId != viewerUserId)
            .OrderByDescending(current => current.RoundNumber)
            .ThenByDescending(current => current.SubmittedAtUtc)
            .FirstOrDefault();

        var waitingForViewer = duel.Status switch
        {
            DuelStatus.Accepted => !duel.ResultSubmissions.Any(current => current.SubmittedByUserId == viewerUserId && current.RoundNumber == 1),
            DuelStatus.AwaitingSecondReview => !duel.ResultSubmissions.Any(current => current.SubmittedByUserId == viewerUserId && current.RoundNumber == 2),
            DuelStatus.CoinFlipInProgress => IsViewerWaitingOnCoinFlip(duel, viewerUserId),
            _ => false
        };

        return new DuelStatusResponse(
            duel.Id,
            duel.ChallengerId,
            duel.Challenger.DisplayName,
            duel.OpponentId,
            duel.Opponent.DisplayName,
            ToView(duel.Status),
            duel.PointsWager,
            currentRound,
            mySubmission is null ? null : ToView(mySubmission.Choice),
            opponentSubmission is null ? null : ToView(opponentSubmission.Choice),
            waitingForViewer,
            ToCoinFlipResponse(duel.CoinFlip, viewerUserId),
            duel.WinnerUserId,
            duel.CreatedAtUtc,
            duel.CompletedAtUtc);
    }

    private static bool IsViewerWaitingOnCoinFlip(Duel duel, Guid viewerUserId)
    {
        var coinFlip = duel.CoinFlip;
        if (coinFlip is null)
        {
            return false;
        }

        if (coinFlip.ResultSide.HasValue)
        {
            return false;
        }

        if (!coinFlip.FirstChooserSide.HasValue)
        {
            return coinFlip.FirstChooserUserId == viewerUserId;
        }

        return coinFlip.SecondChooserUserId == viewerUserId && !coinFlip.SecondChooserSide.HasValue;
    }

    private static DuelCoinFlipResponse? ToCoinFlipResponse(DuelCoinFlip? coinFlip, Guid viewerUserId)
    {
        if (coinFlip is null)
        {
            return null;
        }

        var waitingForFirstChooser = !coinFlip.FirstChooserSide.HasValue && !coinFlip.ResultSide.HasValue;
        var waitingForSecondChooser = coinFlip.FirstChooserSide.HasValue
            && !coinFlip.SecondChooserSide.HasValue
            && !coinFlip.ResultSide.HasValue;

        return new DuelCoinFlipResponse(
            coinFlip.DuelId,
            coinFlip.FirstChooserUserId,
            coinFlip.SecondChooserUserId,
            coinFlip.FirstChooserSide.HasValue ? ToView(coinFlip.FirstChooserSide.Value) : null,
            coinFlip.SecondChooserSide.HasValue ? ToView(coinFlip.SecondChooserSide.Value) : null,
            coinFlip.ResultSide.HasValue ? ToView(coinFlip.ResultSide.Value) : null,
            coinFlip.WinnerUserId,
            waitingForFirstChooser && coinFlip.FirstChooserUserId == viewerUserId,
            waitingForSecondChooser && coinFlip.SecondChooserUserId == viewerUserId,
            coinFlip.ResultSide.HasValue);
    }

    private static DuelStatus ToDomain(DuelStatusView value)
    {
        return value switch
        {
            DuelStatusView.Pending => DuelStatus.Pending,
            DuelStatusView.Declined => DuelStatus.Declined,
            DuelStatusView.Accepted => DuelStatus.Accepted,
            DuelStatusView.AwaitingSecondReview => DuelStatus.AwaitingSecondReview,
            DuelStatusView.CoinFlipInProgress => DuelStatus.CoinFlipInProgress,
            DuelStatusView.Completed => DuelStatus.Completed,
            DuelStatusView.Expired => DuelStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static DuelPlayerResultChoice ToDomain(DuelResultChoiceView value)
    {
        return value switch
        {
            DuelResultChoiceView.Won => DuelPlayerResultChoice.Won,
            DuelResultChoiceView.Lost => DuelPlayerResultChoice.Lost,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static CoinSide ToDomain(CoinSideView value)
    {
        return value switch
        {
            CoinSideView.Heads => CoinSide.Heads,
            CoinSideView.Tails => CoinSide.Tails,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static DuelStatusView ToView(DuelStatus value)
    {
        return value switch
        {
            DuelStatus.Pending => DuelStatusView.Pending,
            DuelStatus.Declined => DuelStatusView.Declined,
            DuelStatus.Accepted => DuelStatusView.Accepted,
            DuelStatus.AwaitingSecondReview => DuelStatusView.AwaitingSecondReview,
            DuelStatus.CoinFlipInProgress => DuelStatusView.CoinFlipInProgress,
            DuelStatus.Completed => DuelStatusView.Completed,
            DuelStatus.Expired => DuelStatusView.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static DuelResultChoiceView ToView(DuelPlayerResultChoice value)
    {
        return value switch
        {
            DuelPlayerResultChoice.Won => DuelResultChoiceView.Won,
            DuelPlayerResultChoice.Lost => DuelResultChoiceView.Lost,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static CoinSideView ToView(CoinSide value)
    {
        return value switch
        {
            CoinSide.Heads => CoinSideView.Heads,
            CoinSide.Tails => CoinSideView.Tails,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
