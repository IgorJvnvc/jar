using System.Net;
using System.Net.Http.Json;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class AuthTests : IntegrationTestBase
{
    public AuthTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_CreatesUserAndReturnsTokens()
    {
        var client = Factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Rack Master",
            email = "rack-master@pool.test",
            password = "Password1"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Created);

        var payload = await TestApi.ReadAsAsync<AuthResponseDto>(response);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal("Rack Master", payload.User.DisplayName);
        Assert.Equal("rack-master@pool.test", payload.User.Email);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = Factory.CreateApiClient();

        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Breaker One",
            email = "duplicate@pool.test",
            password = "Password1"
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Breaker Two",
            email = "duplicate@pool.test",
            password = "Password1"
        });

        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
        var error = await TestApi.ReadErrorAsync(second);
        Assert.NotNull(error);
        Assert.Contains("already exists", error!.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var client = Factory.CreateApiClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Cue Wizard",
            email = "cue-wizard@pool.test",
            password = "Password1"
        });
        await TestApi.EnsureStatusAsync(register, HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "cue-wizard@pool.test",
            password = "Password1"
        });

        await TestApi.EnsureStatusAsync(login, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<AuthResponseDto>(login);
        Assert.Equal("Cue Wizard", payload.User.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Pocket Artist",
            email = "pocket-artist@pool.test",
            password = "Password1"
        });
        await TestApi.EnsureStatusAsync(register, HttpStatusCode.Created);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "pocket-artist@pool.test",
            password = "WrongPassword1"
        });

        await TestApi.EnsureStatusAsync(login, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokens()
    {
        var session = await RegisterAndLoginAsync("Refresher");
        var refresh = await session.Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });

        await TestApi.EnsureStatusAsync(refresh, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<AuthResponseDto>(refresh);

        Assert.NotEqual(session.RefreshToken, payload.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
    }

    [Fact]
    public async Task Revoke_ThenRefresh_ReturnsUnauthorized()
    {
        var session = await RegisterAndLoginAsync("Revoker");

        var revoke = await session.Client.PostAsJsonAsync("/api/auth/revoke", new
        {
            refreshToken = session.RefreshToken
        });
        await TestApi.EnsureStatusAsync(revoke, HttpStatusCode.NoContent);

        var refresh = await session.Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = session.RefreshToken
        });

        await TestApi.EnsureStatusAsync(refresh, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidAccessToken_ReturnsCurrentUser()
    {
        var session = await RegisterAndLoginAsync("WhoAmI");

        var me = await session.Client.GetAsync("/api/auth/me");
        await TestApi.EnsureStatusAsync(me, HttpStatusCode.OK);

        var payload = await TestApi.ReadAsAsync<UserSummaryDto>(me);
        Assert.Equal(session.UserId, payload.Id);
        Assert.Equal(session.Email, payload.Email);
    }
}
