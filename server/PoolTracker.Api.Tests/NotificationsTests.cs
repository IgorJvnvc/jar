using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class NotificationsTests : IntegrationTestBase
{
    public NotificationsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterDevice_StoresTokenForCurrentUser()
    {
        var session = await RegisterAndLoginAsync("NotifyRegister");

        var response = await TestApi.PostAsync(session, "/api/notifications/register-device", new
        {
            token = "token-abc",
            platform = "Android"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.NoContent);

        var stored = await Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.DeviceTokens
                .Select(current => new DeviceTokenRowDto(
                    current.UserId,
                    current.Token,
                    current.Platform.ToString()))
                .SingleAsync());

        Assert.Equal(session.UserId, stored.UserId);
        Assert.Equal("token-abc", stored.Token);
        Assert.Equal("Android", stored.Platform);
    }

    [Fact]
    public async Task UnregisterDevice_RemovesExistingToken()
    {
        var session = await RegisterAndLoginAsync("NotifyUnregister");

        var register = await TestApi.PostAsync(session, "/api/notifications/register-device", new
        {
            token = "token-remove",
            platform = "Android"
        });
        await TestApi.EnsureStatusAsync(register, HttpStatusCode.NoContent);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/unregister-device")
        {
            Content = JsonContent.Create(new
            {
                token = "token-remove"
            })
        };

        var unregister = await session.Client.SendAsync(request);

        await TestApi.EnsureStatusAsync(unregister, HttpStatusCode.NoContent);

        var count = await Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.DeviceTokens.CountAsync());

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RegisterDevice_WithWhitespaceToken_TrimsAndStoresToken()
    {
        var session = await RegisterAndLoginAsync("NotifyTrimmed");

        var response = await TestApi.PostAsync(session, "/api/notifications/register-device", new
        {
            token = "   token-trimmed   ",
            platform = "Ios"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.NoContent);

        var stored = await Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.DeviceTokens
                .Select(current => new DeviceTokenRowDto(
                    current.UserId,
                    current.Token,
                    current.Platform.ToString()))
                .SingleAsync());

        Assert.Equal(session.UserId, stored.UserId);
        Assert.Equal("token-trimmed", stored.Token);
        Assert.Equal("Ios", stored.Platform);
    }

    [Fact]
    public async Task RegisterDevice_WithInvalidPlatform_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("NotifyInvalidPlatform");

        var response = await TestApi.PostAsync(session, "/api/notifications/register-device", new
        {
            token = "token-invalid-platform",
            platform = "Desktop"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterDevice_WhenTokenAlreadyExists_MovesTokenToCurrentUser()
    {
        var firstUser = await RegisterAndLoginAsync("NotifyMoveA");
        var secondUser = await RegisterAndLoginAsync("NotifyMoveB");

        var firstRegister = await TestApi.PostAsync(firstUser, "/api/notifications/register-device", new
        {
            token = "shared-token",
            platform = "Android"
        });
        await TestApi.EnsureStatusAsync(firstRegister, HttpStatusCode.NoContent);

        var secondRegister = await TestApi.PostAsync(secondUser, "/api/notifications/register-device", new
        {
            token = "shared-token",
            platform = "Ios"
        });
        await TestApi.EnsureStatusAsync(secondRegister, HttpStatusCode.NoContent);

        var stored = await Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.DeviceTokens
                .Select(current => new DeviceTokenRowDto(
                    current.UserId,
                    current.Token,
                    current.Platform.ToString()))
                .SingleAsync());

        Assert.Equal(secondUser.UserId, stored.UserId);
        Assert.Equal("shared-token", stored.Token);
        Assert.Equal("Ios", stored.Platform);
    }

    [Fact]
    public async Task UnregisterDevice_WithWhitespaceToken_RemovesToken()
    {
        var session = await RegisterAndLoginAsync("NotifyUnregisterTrim");

        var register = await TestApi.PostAsync(session, "/api/notifications/register-device", new
        {
            token = "token-trim-remove",
            platform = "Android"
        });
        await TestApi.EnsureStatusAsync(register, HttpStatusCode.NoContent);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/unregister-device")
        {
            Content = JsonContent.Create(new
            {
                token = "   token-trim-remove   "
            })
        };

        var unregister = await session.Client.SendAsync(request);
        await TestApi.EnsureStatusAsync(unregister, HttpStatusCode.NoContent);

        var count = await Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.DeviceTokens.CountAsync());

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RegisterDevice_ForUnauthenticatedUser_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/notifications/register-device", new
        {
            token = "token-anon",
            platform = "Android"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnregisterDevice_ForUnauthenticatedUser_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/unregister-device")
        {
            Content = JsonContent.Create(new
            {
                token = "token-anon"
            })
        };

        var response = await client.SendAsync(request);

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Unauthorized);
    }
}
