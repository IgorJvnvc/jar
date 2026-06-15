using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Profile.Contracts;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Features.Profile;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly PoolTrackerDbContext dbContext;
    private readonly IPointsLedgerService pointsLedger;

    public ProfileController(PoolTrackerDbContext dbContext, IPointsLedgerService pointsLedger)
    {
        this.dbContext = dbContext;
        this.pointsLedger = pointsLedger;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var profile = await pointsLedger.GetOrCreateProfileAsync(user.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user, profile));
    }

    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMyProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(current => current.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var profile = await pointsLedger.GetOrCreateProfileAsync(user.Id, cancellationToken);

        user.DisplayName = request.DisplayName.Trim();
        profile.AvatarColorHex = request.AvatarColorHex;
        profile.FavoriteBallNumber = request.FavoriteBallNumber;
        profile.Power = request.Power;
        profile.Accuracy = request.Accuracy;
        profile.CueControl = request.CueControl;
        profile.Spin = request.Spin;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(user, profile));
    }

    [HttpPost("pay-debt")]
    public async Task<ActionResult<PayDebtResponse>> PayDebt(PayDebtRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(current => current.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var paidPoints = await pointsLedger.PayDebtAsync(userId.Value, request.Amount, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var profile = await pointsLedger.GetOrCreateProfileAsync(userId.Value, cancellationToken);

        return Ok(new PayDebtResponse(paidPoints, ToResponse(user, profile)));
    }

    private static ProfileResponse ToResponse(ApplicationUser user, PlayerProfile profile)
    {
        return new ProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            profile.AvatarColorHex,
            profile.FavoriteBallNumber,
            profile.Points,
            profile.DebtPoints,
            profile.Title,
            profile.Power,
            profile.Accuracy,
            profile.CueControl,
            profile.Spin,
            profile.UpdatedAtUtc);
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
