using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

public interface IPointsLedgerService
{
    Task AwardPointsAsync(
        Guid userId,
        int points,
        PointsTransactionType type,
        string description,
        Guid? relatedEntityId,
        CancellationToken cancellationToken);

    Task DeductPointsAllowDebtAsync(
        Guid userId,
        int points,
        PointsTransactionType type,
        string description,
        Guid? relatedEntityId,
        CancellationToken cancellationToken);

    Task<int> PayDebtAsync(Guid userId, int amount, CancellationToken cancellationToken);

    Task<PlayerProfile> GetOrCreateProfileAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class PointsLedgerService : IPointsLedgerService
{
    private readonly PoolTrackerDbContext dbContext;

    public PointsLedgerService(PoolTrackerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task AwardPointsAsync(
        Guid userId,
        int points,
        PointsTransactionType type,
        string description,
        Guid? relatedEntityId,
        CancellationToken cancellationToken)
    {
        if (points <= 0)
        {
            return;
        }

        var profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        profile.Points += points;

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        dbContext.PointsTransactions.Add(new PointsTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = points,
            Type = type,
            Description = description,
            RelatedEntityId = relatedEntityId
        });
    }

    public async Task DeductPointsAllowDebtAsync(
        Guid userId,
        int points,
        PointsTransactionType type,
        string description,
        Guid? relatedEntityId,
        CancellationToken cancellationToken)
    {
        if (points <= 0)
        {
            return;
        }

        var profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        var debtIncrease = Math.Max(points - profile.Points, 0);

        profile.Points = Math.Max(profile.Points - points, 0);
        profile.DebtPoints += debtIncrease;
        if (profile.DebtPoints > 0)
        {
            profile.Title = PlayerProfile.DebtTitle;
        }

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        dbContext.PointsTransactions.Add(new PointsTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = -points,
            Type = type,
            Description = description,
            RelatedEntityId = relatedEntityId
        });
    }

    public async Task<int> PayDebtAsync(Guid userId, int amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        if (profile.DebtPoints <= 0 || profile.Points <= 0)
        {
            return 0;
        }

        var payment = Math.Min(amount, Math.Min(profile.DebtPoints, profile.Points));
        if (payment <= 0)
        {
            return 0;
        }

        profile.Points -= payment;
        profile.DebtPoints -= payment;
        if (profile.DebtPoints <= 0)
        {
            profile.DebtPoints = 0;
            if (profile.Title == PlayerProfile.DebtTitle)
            {
                profile.Title = null;
            }
        }

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        dbContext.PointsTransactions.Add(new PointsTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PointsDelta = -payment,
            Type = PointsTransactionType.DebtPayment,
            Description = "Paid off debt",
            RelatedEntityId = null
        });

        return payment;
    }

    public async Task<PlayerProfile> GetOrCreateProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Check the change tracker first so repeated calls within a single unit of work
        // (e.g. crediting a winner and debiting a loser before SaveChanges) reuse the same
        // pending profile instead of attempting a second insert against the unique UserId index.
        var profile = dbContext.PlayerProfiles.Local
            .FirstOrDefault(current => current.UserId == userId)
            ?? await dbContext.PlayerProfiles
                .SingleOrDefaultAsync(current => current.UserId == userId, cancellationToken);

        if (profile is not null)
        {
            return profile;
        }

        profile = new PlayerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId
        };

        dbContext.PlayerProfiles.Add(profile);
        return profile;
    }
}
