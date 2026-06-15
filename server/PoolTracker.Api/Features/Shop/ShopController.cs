using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Shop.Contracts;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Features.Shop;

[ApiController]
[Authorize]
[Route("api/shop")]
public sealed class ShopController : ControllerBase
{
    private readonly PoolTrackerDbContext dbContext;
    private readonly IPointsLedgerService pointsLedger;

    public ShopController(PoolTrackerDbContext dbContext, IPointsLedgerService pointsLedger)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
    }

    [HttpGet("cues")]
    public async Task<ActionResult<IReadOnlyList<CueItemResponse>>> GetCueCatalog(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var cues = await dbContext.CueItems
            .AsNoTracking()
            .Where(cue => cue.IsActive)
            .OrderBy(cue => cue.Rarity)
            .ThenBy(cue => cue.ShopCost ?? int.MaxValue)
            .ThenBy(cue => cue.Name)
            .ToListAsync(cancellationToken);

        var inventory = await dbContext.UserCueInventories
            .AsNoTracking()
            .Where(item => item.UserId == userId.Value)
            .ToDictionaryAsync(item => item.CueItemId, cancellationToken);

        var response = cues.Select(cue =>
        {
            var owned = inventory.TryGetValue(cue.Id, out var inventoryItem);

            return new CueItemResponse(
                cue.Id,
                cue.Name,
                cue.ColorHex,
                cue.Rarity,
                cue.ShopCost,
                cue.AchievementCode,
                cue.PowerBonus,
                cue.AccuracyBonus,
                cue.CueControlBonus,
                cue.SpinBonus,
                owned,
                inventoryItem?.IsEquipped == true);
        }).ToList();

        return Ok(response);
    }

    [HttpPost("cues/purchase")]
    public async Task<ActionResult> PurchaseCue(PurchaseCueRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var cue = await dbContext.CueItems
            .SingleOrDefaultAsync(current => current.Id == request.CueItemId && current.IsActive, cancellationToken);

        if (cue is null)
        {
            return NotFound(new { message = "Cue not found." });
        }

        if (cue.ShopCost is null)
        {
            return BadRequest(new { message = "This cue can only be unlocked through achievements." });
        }

        var alreadyOwned = await dbContext.UserCueInventories
            .AnyAsync(item => item.UserId == userId.Value && item.CueItemId == cue.Id, cancellationToken);

        if (alreadyOwned)
        {
            return Conflict(new { message = "Cue is already in your inventory." });
        }

        var profile = await pointsLedger.GetOrCreateProfileAsync(userId.Value, cancellationToken);

        if (profile.Points < cue.ShopCost.Value)
        {
            return BadRequest(new { message = "Not enough points." });
        }

        profile.Points -= cue.ShopCost.Value;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        dbContext.UserCueInventories.Add(new UserCueInventory
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            CueItemId = cue.Id
        });

        dbContext.PointsTransactions.Add(new PointsTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            PointsDelta = -cue.ShopCost.Value,
            Type = PointsTransactionType.ShopPurchase,
            Description = $"Purchased cue: {cue.Name}",
            RelatedEntityId = cue.Id
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Cue purchased." });
    }

    [HttpPost("cues/equip")]
    public async Task<ActionResult> EquipCue(EquipCueRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var inventoryItem = await dbContext.UserCueInventories
            .Include(item => item.CueItem)
            .SingleOrDefaultAsync(item => item.UserId == userId.Value && item.CueItemId == request.CueItemId, cancellationToken);

        if (inventoryItem is null)
        {
            return NotFound(new { message = "Cue is not in your inventory." });
        }

        var userItems = await dbContext.UserCueInventories
            .Where(item => item.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        foreach (var item in userItems)
        {
            item.IsEquipped = item.Id == inventoryItem.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Cue equipped." });
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
