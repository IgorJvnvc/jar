using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Features.Auth.Contracts;

namespace PoolTracker.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly PoolTrackerDbContext dbContext;
    private readonly IJwtTokenService jwtTokenService;
    private readonly JwtOptions jwtOptions;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        PoolTrackerDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions)
    {
        this.userManager = userManager;
        this.signInManager = signInManager;
        this.dbContext = dbContext;
        this.jwtTokenService = jwtTokenService;
        this.jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Could not create account.",
                errors = createResult.Errors.Select(error => error.Description)
            });
        }

        var response = await IssueTokensAsync(user, cancellationToken);
        return CreatedAtAction(nameof(Me), null, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var checkResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!checkResult.Succeeded)
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var response = await IssueTokensAsync(user, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        var now = DateTimeOffset.UtcNow;
        if (refreshToken.RevokedAtUtc.HasValue || refreshToken.ExpiresAtUtc.UtcDateTime <= now.UtcDateTime)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        refreshToken.RevokedAtUtc = now;

        var user = refreshToken.User;
        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.GenerateAccessToken(user);

        var nextRefreshToken = jwtTokenService.GenerateRefreshToken();
        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = nextRefreshToken,
            ExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays)
        };

        dbContext.RefreshTokens.Add(replacement);
        user.LastSeenAtUtc = now;

        refreshToken.ReplacedByToken = nextRefreshToken;
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken,
            nextRefreshToken,
            accessTokenExpiresAtUtc,
            new UserSummary(user.Id, user.DisplayName, user.Email ?? string.Empty));

        return Ok(response);
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<ActionResult> Revoke(RevokeRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.Token == request.RefreshToken && token.UserId == userId.Value, cancellationToken);

        if (refreshToken is null)
        {
            return NotFound(new { message = "Refresh token not found." });
        }

        if (refreshToken.IsRevoked)
        {
            return NoContent();
        }

        refreshToken.RevokedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserSummary>> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await userManager.Users
            .Where(current => current.Id == userId.Value)
            .Select(current => new UserSummary(current.Id, current.DisplayName, current.Email!))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? Unauthorized() : Ok(user);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.GenerateAccessToken(user);

        var refreshTokenValue = jwtTokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenDays)
        };

        dbContext.RefreshTokens.Add(refreshToken);
        user.LastSeenAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            accessTokenExpiresAtUtc,
            new UserSummary(user.Id, user.DisplayName, user.Email ?? string.Empty));
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
