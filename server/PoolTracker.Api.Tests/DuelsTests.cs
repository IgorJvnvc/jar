using System.Net;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class DuelsTests : IntegrationTestBase
{
    public DuelsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateDuel_CreatesPendingChallenge()
    {
        var challenger = await RegisterAndLoginAsync("DuelCreateA");
        var opponent = await RegisterAndLoginAsync("DuelCreateB");

        var create = await TestApi.PostAsync(challenger, "/api/duels", new
        {
            opponentUserId = opponent.UserId
        });

        await TestApi.EnsureStatusAsync(create, HttpStatusCode.Created);
        var duel = await TestApi.ReadAsAsync<DuelStatusResponseDto>(create);

        Assert.Equal("Pending", duel.Status);
        Assert.Equal(100, duel.PointsWager);
        Assert.Equal(challenger.UserId, duel.ChallengerId);
        Assert.Equal(opponent.UserId, duel.OpponentId);
    }

    [Fact]
    public async Task CreateDuel_WithSelf_ReturnsBadRequest()
    {
        var player = await RegisterAndLoginAsync("DuelSelf");

        var create = await TestApi.PostAsync(player, "/api/duels", new
        {
            opponentUserId = player.UserId
        });

        await TestApi.EnsureStatusAsync(create, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDuel_WhenOpenDuelExists_ReturnsConflict()
    {
        var challenger = await RegisterAndLoginAsync("DuelDupA");
        var opponent = await RegisterAndLoginAsync("DuelDupB");

        var first = await TestApi.PostAsync(challenger, "/api/duels", new
        {
            opponentUserId = opponent.UserId
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.Created);

        var second = await TestApi.PostAsync(challenger, "/api/duels", new
        {
            opponentUserId = opponent.UserId
        });

        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RespondDuel_Accept_TransitionsToAccepted()
    {
        var challenger = await RegisterAndLoginAsync("DuelAcceptA");
        var opponent = await RegisterAndLoginAsync("DuelAcceptB");
        var duel = await CreateDuelAsync(challenger, opponent.UserId);

        var respond = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/respond", new
        {
            accept = true
        });

        await TestApi.EnsureStatusAsync(respond, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelStatusResponseDto>(respond);
        Assert.Equal("Accepted", payload.Status);
    }

    [Fact]
    public async Task RespondDuel_Decline_TransitionsToDeclined()
    {
        var challenger = await RegisterAndLoginAsync("DuelDeclineA");
        var opponent = await RegisterAndLoginAsync("DuelDeclineB");
        var duel = await CreateDuelAsync(challenger, opponent.UserId);

        var respond = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/respond", new
        {
            accept = false
        });

        await TestApi.EnsureStatusAsync(respond, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelStatusResponseDto>(respond);
        Assert.Equal("Declined", payload.Status);
    }

    [Fact]
    public async Task SubmitResult_WhenBothAgree_CompletesDuelAndTransfersPoints()
    {
        var winner = await RegisterAndLoginAsync("DuelAgreeWinner");
        var loser = await RegisterAndLoginAsync("DuelAgreeLoser");
        await SeedProfileAsync(winner.UserId, points: 25, debt: 0, title: null);
        await SeedProfileAsync(loser.UserId, points: 125, debt: 0, title: null);

        var duel = await CreateAndAcceptDuelAsync(winner, loser);

        var firstSubmit = await TestApi.PostAsync(winner, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Won"
        });
        await TestApi.EnsureStatusAsync(firstSubmit, HttpStatusCode.OK);

        var secondSubmit = await TestApi.PostAsync(loser, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Lost"
        });

        await TestApi.EnsureStatusAsync(secondSubmit, HttpStatusCode.OK);
        var completed = await TestApi.ReadAsAsync<DuelStatusResponseDto>(secondSubmit);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(winner.UserId, completed.WinnerUserId);

        var winnerProfile = await GetProfileAsync(winner);
        var loserProfile = await GetProfileAsync(loser);

        Assert.Equal(125, winnerProfile.Points);
        Assert.Equal(25, loserProfile.Points);
        Assert.Equal(0, loserProfile.DebtPoints);
    }

    [Fact]
    public async Task SubmitResult_FirstRoundMismatch_RequiresSecondReview()
    {
        var challenger = await RegisterAndLoginAsync("DuelReviewA");
        var opponent = await RegisterAndLoginAsync("DuelReviewB");
        await SeedProfileAsync(challenger.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(opponent.UserId, points: 100, debt: 0, title: null);

        var duel = await CreateAndAcceptDuelAsync(challenger, opponent);

        var first = await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Won"
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.OK);

        var second = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Won"
        });
        await TestApi.EnsureStatusAsync(second, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelStatusResponseDto>(second);

        Assert.Equal("AwaitingSecondReview", payload.Status);
        Assert.Equal(2, payload.CurrentRound);
    }

    [Fact]
    public async Task SubmitResult_SecondRoundMismatch_StartsCoinFlip()
    {
        var challenger = await RegisterAndLoginAsync("CoinStartA");
        var opponent = await RegisterAndLoginAsync("CoinStartB");
        await SeedProfileAsync(challenger.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(opponent.UserId, points: 100, debt: 0, title: null);

        var duel = await CreateAndAcceptDuelAsync(challenger, opponent);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        var secondRound = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Won"
        });

        await TestApi.EnsureStatusAsync(secondRound, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelStatusResponseDto>(secondRound);

        Assert.Equal("CoinFlipInProgress", payload.Status);
        Assert.NotNull(payload.CoinFlip);
        Assert.Equal(challenger.UserId, payload.CoinFlip!.FirstChooserUserId);
    }

    [Fact]
    public async Task CoinFlip_SecondChooserBeforeFirst_ReturnsConflict()
    {
        var challenger = await RegisterAndLoginAsync("CoinOrderA");
        var opponent = await RegisterAndLoginAsync("CoinOrderB");
        await SeedProfileAsync(challenger.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(opponent.UserId, points: 100, debt: 0, title: null);

        var duel = await StartCoinFlipStateAsync(challenger, opponent);

        var pick = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/coin-flip/choose", new
        {
            side = "Heads"
        });

        await TestApi.EnsureStatusAsync(pick, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CoinFlip_ResolvesDuelAndTransfersPoints()
    {
        var challenger = await RegisterAndLoginAsync("CoinResolveA");
        var opponent = await RegisterAndLoginAsync("CoinResolveB");
        await SeedProfileAsync(challenger.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(opponent.UserId, points: 100, debt: 0, title: null);

        var duel = await StartCoinFlipStateAsync(challenger, opponent);

        var firstPick = await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/coin-flip/choose", new
        {
            side = "Heads"
        });
        await TestApi.EnsureStatusAsync(firstPick, HttpStatusCode.OK);

        var secondPick = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/coin-flip/choose", new
        {
            side = "Tails"
        });

        await TestApi.EnsureStatusAsync(secondPick, HttpStatusCode.OK);
        var completed = await TestApi.ReadAsAsync<DuelStatusResponseDto>(secondPick);

        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CoinFlip);
        Assert.True(completed.CoinFlip!.IsResolved);
        Assert.NotNull(completed.WinnerUserId);
        Assert.NotNull(completed.CoinFlip.ResultSide);

        var winner = completed.WinnerUserId.Value;
        var loser = winner == challenger.UserId ? opponent.UserId : challenger.UserId;

        var pointsState = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var winnerProfile = await dbContext.PlayerProfiles.SingleAsync(current => current.UserId == winner);
            var loserProfile = await dbContext.PlayerProfiles.SingleAsync(current => current.UserId == loser);

            return (WinnerPoints: winnerProfile.Points, LoserPoints: loserProfile.Points);
        });

        Assert.Equal(200, pointsState.WinnerPoints);
        Assert.Equal(0, pointsState.LoserPoints);
    }

    [Fact]
    public async Task SubmitResult_WhenBothAgree_RecordsDuelWinAndLoss()
    {
        var winner = await RegisterAndLoginAsync("DuelRecordWinner");
        var loser = await RegisterAndLoginAsync("DuelRecordLoser");
        await SeedProfileAsync(winner.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(loser.UserId, points: 100, debt: 0, title: null);

        var duel = await CreateAndAcceptDuelAsync(winner, loser);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(winner, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(loser, $"/api/duels/{duel.Id}/submit-result", new { choice = "Lost" }), HttpStatusCode.OK);

        var winnerProfile = await GetProfileAsync(winner);
        var loserProfile = await GetProfileAsync(loser);

        Assert.Equal(1, winnerProfile.DuelsWon);
        Assert.Equal(0, winnerProfile.DuelsLost);
        Assert.Equal(0, loserProfile.DuelsWon);
        Assert.Equal(1, loserProfile.DuelsLost);
    }

    [Fact]
    public async Task CoinFlip_ResolvesDuel_RecordsDuelWinAndLoss()
    {
        var challenger = await RegisterAndLoginAsync("CoinRecordA");
        var opponent = await RegisterAndLoginAsync("CoinRecordB");
        await SeedProfileAsync(challenger.UserId, points: 100, debt: 0, title: null);
        await SeedProfileAsync(opponent.UserId, points: 100, debt: 0, title: null);

        var duel = await StartCoinFlipStateAsync(challenger, opponent);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/coin-flip/choose", new { side = "Heads" }), HttpStatusCode.OK);
        var secondPick = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/coin-flip/choose", new { side = "Tails" });
        await TestApi.EnsureStatusAsync(secondPick, HttpStatusCode.OK);
        var completed = await TestApi.ReadAsAsync<DuelStatusResponseDto>(secondPick);

        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.WinnerUserId);

        var winnerSession = completed.WinnerUserId!.Value == challenger.UserId ? challenger : opponent;
        var loserSession = completed.WinnerUserId!.Value == challenger.UserId ? opponent : challenger;

        var winnerProfile = await GetProfileAsync(winnerSession);
        var loserProfile = await GetProfileAsync(loserSession);

        Assert.Equal(1, winnerProfile.DuelsWon);
        Assert.Equal(0, winnerProfile.DuelsLost);
        Assert.Equal(0, loserProfile.DuelsWon);
        Assert.Equal(1, loserProfile.DuelsLost);
    }

    [Fact]
    public async Task DuelLoss_WhenInsufficientPoints_CreatesDebtAndAppliesTitle()
    {
        var winner = await RegisterAndLoginAsync("DebtWinner");
        var loser = await RegisterAndLoginAsync("DebtLoser");
        await SeedProfileAsync(winner.UserId, points: 50, debt: 0, title: null);
        await SeedProfileAsync(loser.UserId, points: 20, debt: 0, title: null);

        var duel = await CreateAndAcceptDuelAsync(winner, loser);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(winner, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        var complete = await TestApi.PostAsync(loser, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Lost"
        });

        await TestApi.EnsureStatusAsync(complete, HttpStatusCode.OK);

        var loserProfile = await GetProfileAsync(loser);
        Assert.Equal(0, loserProfile.Points);
        Assert.Equal(80, loserProfile.DebtPoints);
        Assert.Equal(PlayerProfile.DebtTitle, loserProfile.Title);
    }

    [Fact]
    public async Task ExpiredPendingDuel_IsMarkedExpiredOnList()
    {
        var challenger = await RegisterAndLoginAsync("ExpiredA");
        var opponent = await RegisterAndLoginAsync("ExpiredB");
        var duel = await CreateDuelAsync(challenger, opponent.UserId);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Duels.SingleAsync(current => current.Id == duel.Id);
            entity.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        });

        var list = await TestApi.GetAsync(challenger, "/api/duels");
        await TestApi.EnsureStatusAsync(list, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelListResponseDto>(list);
        var expired = payload.Items.Single(current => current.Id == duel.Id);

        Assert.Equal("Expired", expired.Status);
    }

    [Fact]
    public async Task ListDuels_FilterByStatus_ReturnsOnlyMatchingRows()
    {
        var challenger = await RegisterAndLoginAsync("FilterA");
        var opponent = await RegisterAndLoginAsync("FilterB");

        var duel = await CreateDuelAsync(challenger, opponent.UserId);
        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/respond", new { accept = false }), HttpStatusCode.OK);

        var declined = await TestApi.GetAsync(challenger, "/api/duels?status=Declined");
        await TestApi.EnsureStatusAsync(declined, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<DuelListResponseDto>(declined);

        Assert.Single(payload.Items);
        Assert.Equal("Declined", payload.Items[0].Status);
    }

    [Fact]
    public async Task GetDuel_ByNonParticipant_ReturnsForbidden()
    {
        var challenger = await RegisterAndLoginAsync("ForbiddenA");
        var opponent = await RegisterAndLoginAsync("ForbiddenB");
        var stranger = await RegisterAndLoginAsync("ForbiddenC");
        var duel = await CreateDuelAsync(challenger, opponent.UserId);

        var response = await TestApi.GetAsync(stranger, $"/api/duels/{duel.Id}");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Forbidden);
    }

    private async Task<DuelStatusResponseDto> CreateDuelAsync(TestAuthSession challenger, Guid opponentUserId)
    {
        var response = await TestApi.PostAsync(challenger, "/api/duels", new
        {
            opponentUserId
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Created);
        return await TestApi.ReadAsAsync<DuelStatusResponseDto>(response);
    }

    private async Task<DuelStatusResponseDto> CreateAndAcceptDuelAsync(TestAuthSession challenger, TestAuthSession opponent)
    {
        var duel = await CreateDuelAsync(challenger, opponent.UserId);
        var accept = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/respond", new { accept = true });
        await TestApi.EnsureStatusAsync(accept, HttpStatusCode.OK);
        return duel;
    }

    private async Task<DuelStatusResponseDto> StartCoinFlipStateAsync(TestAuthSession challenger, TestAuthSession opponent)
    {
        var duel = await CreateAndAcceptDuelAsync(challenger, opponent);

        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);
        await TestApi.EnsureStatusAsync(await TestApi.PostAsync(challenger, $"/api/duels/{duel.Id}/submit-result", new { choice = "Won" }), HttpStatusCode.OK);

        var secondRound = await TestApi.PostAsync(opponent, $"/api/duels/{duel.Id}/submit-result", new
        {
            choice = "Won"
        });

        await TestApi.EnsureStatusAsync(secondRound, HttpStatusCode.OK);
        return await TestApi.ReadAsAsync<DuelStatusResponseDto>(secondRound);
    }

    private async Task SeedProfileAsync(Guid userId, int points, int debt, string? title)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);
            if (profile is null)
            {
                profile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AvatarColorHex = "#1d7a59",
                    Power = 50,
                    Accuracy = 50,
                    CueControl = 50,
                    Spin = 50
                };

                dbContext.PlayerProfiles.Add(profile);
            }

            profile.Points = points;
            profile.DebtPoints = debt;
            profile.Title = title;

            await dbContext.SaveChangesAsync();
        });
    }

    private static async Task<ProfileResponseDto> GetProfileAsync(TestAuthSession session)
    {
        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        return await TestApi.ReadAsAsync<ProfileResponseDto>(response);
    }
}
