using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class SignalRNotificationTests : IntegrationTestBase
{
    public SignalRNotificationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SessionEvents_AreBroadcastToConnectedClients()
    {
        var actor = await RegisterAndLoginAsync("SignalRSessionActor");
        var listener = await RegisterAndLoginAsync("SignalRSessionListener");

        var hall = await TestApi.PostAsync(actor, "/api/halls", new
        {
            name = "SignalR Hall",
            address = "Live Street",
            latitude = 44.8,
            longitude = 20.4,
            totalTables = 8
        });
        await TestApi.EnsureSuccessAsync(hall);
        var hallPayload = await TestApi.ReadAsAsync<PoolHallResponseDto>(hall);

        var startedEvents = new ConcurrentQueue<object>();
        var endedEvents = new ConcurrentQueue<object>();

        await using var hubConnection = BuildHubConnection(listener.AccessToken);
        hubConnection.On<object>("SessionStarted", payload => startedEvents.Enqueue(payload));
        hubConnection.On<object>("SessionEnded", payload => endedEvents.Enqueue(payload));

        await hubConnection.StartAsync();

        var start = await TestApi.PostAsync(actor, "/api/sessions/start", new
        {
            poolHallId = hallPayload.Id,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureSuccessAsync(start);
        var startedSession = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Task.Delay(350);

        var end = await TestApi.PostAsync(actor, $"/api/sessions/{startedSession.Id}/end", new
        {
            ballsPotted = 12,
            gamesWon = 1,
            gamesLost = 0,
            snookersEscaped = 0,
            notes = "signalr"
        });
        await TestApi.EnsureSuccessAsync(end);

        await Task.Delay(350);

        Assert.True(startedEvents.Count >= 1);
        Assert.True(endedEvents.Count >= 1);
    }

    [Fact]
    public async Task DuelEvents_AreSentToOpponentAndChallenger()
    {
        var challenger = await RegisterAndLoginAsync("SignalRDuelA");
        var opponent = await RegisterAndLoginAsync("SignalRDuelB");

        var challengeEvents = new ConcurrentQueue<object>();
        var responseEvents = new ConcurrentQueue<object>();

        await using var opponentHub = BuildHubConnection(opponent.AccessToken);
        await using var challengerHub = BuildHubConnection(challenger.AccessToken);

        opponentHub.On<object>("DuelChallengeReceived", payload => challengeEvents.Enqueue(payload));
        challengerHub.On<object>("DuelResponseReceived", payload => responseEvents.Enqueue(payload));

        await opponentHub.StartAsync();
        await challengerHub.StartAsync();

        var create = await TestApi.PostAsync(challenger, "/api/duels", new
        {
            opponentUserId = opponent.UserId
        });
        await TestApi.EnsureSuccessAsync(create);
        var duel = await TestApi.ReadAsAsync<DuelStatusResponseDto>(create);

        await Task.Delay(300);

        var respond = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/respond", new
        {
            accept = true
        });
        await TestApi.EnsureSuccessAsync(respond);

        await Task.Delay(300);

        Assert.True(challengeEvents.Count >= 1);
        Assert.True(responseEvents.Count >= 1);
    }

    private HubConnection BuildHubConnection(string accessToken)
    {
        var client = Factory.CreateApiClient();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
    }
}
