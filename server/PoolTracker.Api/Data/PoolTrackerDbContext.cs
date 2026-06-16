using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Data;

public sealed class PoolTrackerDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public PoolTrackerDbContext(DbContextOptions<PoolTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<PoolHall> PoolHalls => Set<PoolHall>();

    public DbSet<PoolHallTable> PoolHallTables => Set<PoolHallTable>();

    public DbSet<PoolHallRating> PoolHallRatings => Set<PoolHallRating>();

    public DbSet<PoolHallTableRating> PoolHallTableRatings => Set<PoolHallTableRating>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SessionReport> SessionReports => Set<SessionReport>();

    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();

    public DbSet<PlayerDailyMetric> PlayerDailyMetrics => Set<PlayerDailyMetric>();

    public DbSet<CueItem> CueItems => Set<CueItem>();

    public DbSet<UserCueInventory> UserCueInventories => Set<UserCueInventory>();

    public DbSet<Duel> Duels => Set<Duel>();

    public DbSet<DuelResultSubmission> DuelResultSubmissions => Set<DuelResultSubmission>();

    public DbSet<DuelCoinFlip> DuelCoinFlips => Set<DuelCoinFlip>();

    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    public DbSet<HallDayCompetition> HallDayCompetitions => Set<HallDayCompetition>();

    public DbSet<HallDayCompetitionEntry> HallDayCompetitionEntries => Set<HallDayCompetitionEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(60)
                .IsRequired();

            entity.Property(user => user.CreatedAtUtc)
                .HasDefaultValueSql("now()");
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);

            entity.Property(token => token.Token)
                .HasMaxLength(160)
                .IsRequired();

            entity.HasIndex(token => token.Token)
                .IsUnique();

            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PlayerProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);

            entity.Property(profile => profile.AvatarColorHex)
                .HasMaxLength(9)
                .IsRequired();

            entity.Property(profile => profile.Power)
                .HasPrecision(5, 2);

            entity.Property(profile => profile.Accuracy)
                .HasPrecision(5, 2);

            entity.Property(profile => profile.CueControl)
                .HasPrecision(5, 2);

            entity.Property(profile => profile.Spin)
                .HasPrecision(5, 2);

            entity.Property(profile => profile.Title)
                .HasMaxLength(60);

            entity.HasIndex(profile => profile.UserId)
                .IsUnique();

            entity.HasOne(profile => profile.User)
                .WithOne()
                .HasForeignKey<PlayerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PoolHall>(entity =>
        {
            entity.HasKey(hall => hall.Id);

            entity.Property(hall => hall.Name)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(hall => hall.Address)
                .HasMaxLength(260)
                .IsRequired();

            entity.HasOne(hall => hall.AddedByUser)
                .WithMany()
                .HasForeignKey(hall => hall.AddedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PoolHallTable>(entity =>
        {
            entity.HasKey(table => table.Id);

            entity.Property(table => table.TableLabel)
                .HasMaxLength(80)
                .IsRequired();

            entity.HasIndex(table => new { table.PoolHallId, table.TableLabel })
                .IsUnique();

            entity.HasOne(table => table.PoolHall)
                .WithMany(hall => hall.Tables)
                .HasForeignKey(table => table.PoolHallId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(table => table.AddedByUser)
                .WithMany()
                .HasForeignKey(table => table.AddedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PoolHallRating>(entity =>
        {
            entity.HasKey(rating => rating.Id);

            entity.Property(rating => rating.OverallScore)
                .HasPrecision(4, 2);

            entity.Property(rating => rating.Comment)
                .HasMaxLength(500);

            entity.HasIndex(rating => new { rating.PoolHallId, rating.UserId, rating.RatingDate })
                .IsUnique();

            entity.HasOne(rating => rating.PoolHall)
                .WithMany(hall => hall.Ratings)
                .HasForeignKey(rating => rating.PoolHallId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rating => rating.User)
                .WithMany()
                .HasForeignKey(rating => rating.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PoolHallTableRating>(entity =>
        {
            entity.HasKey(rating => rating.Id);

            entity.Property(rating => rating.OverallScore)
                .HasPrecision(4, 2);

            entity.Property(rating => rating.Comment)
                .HasMaxLength(500);

            entity.HasIndex(rating => new { rating.PoolHallTableId, rating.UserId, rating.RatingDate })
                .IsUnique();

            entity.HasOne(rating => rating.PoolHallTable)
                .WithMany(table => table.Ratings)
                .HasForeignKey(rating => rating.PoolHallTableId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rating => rating.User)
                .WithMany()
                .HasForeignKey(rating => rating.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Session>(entity =>
        {
            entity.HasKey(session => session.Id);

            entity.HasIndex(session => new { session.UserId, session.IsActive })
                .HasFilter("\"IsActive\" = true");

            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(session => session.PoolHall)
                .WithMany()
                .HasForeignKey(session => session.PoolHallId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(session => session.PoolHallTable)
                .WithMany()
                .HasForeignKey(session => session.PoolHallTableId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SessionReport>(entity =>
        {
            entity.HasKey(report => report.SessionId);

            entity.Property(report => report.Notes)
                .HasMaxLength(750);

            entity.HasOne(report => report.Session)
                .WithOne(session => session.Report)
                .HasForeignKey<SessionReport>(report => report.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PointsTransaction>(entity =>
        {
            entity.HasKey(transaction => transaction.Id);

            entity.Property(transaction => transaction.Description)
                .HasMaxLength(180)
                .IsRequired();

            entity.HasOne(transaction => transaction.User)
                .WithMany()
                .HasForeignKey(transaction => transaction.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlayerDailyMetric>(entity =>
        {
            entity.HasKey(metric => metric.Id);

            entity.HasIndex(metric => new { metric.UserId, metric.Date })
                .IsUnique();

            entity.HasOne(metric => metric.User)
                .WithMany()
                .HasForeignKey(metric => metric.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CueItem>(entity =>
        {
            entity.HasKey(cue => cue.Id);

            entity.Property(cue => cue.Name)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(cue => cue.ColorHex)
                .HasMaxLength(9)
                .IsRequired();

            entity.Property(cue => cue.AchievementCode)
                .HasMaxLength(100);

            entity.Property(cue => cue.PowerBonus)
                .HasPrecision(5, 2);

            entity.Property(cue => cue.AccuracyBonus)
                .HasPrecision(5, 2);

            entity.Property(cue => cue.CueControlBonus)
                .HasPrecision(5, 2);

            entity.Property(cue => cue.SpinBonus)
                .HasPrecision(5, 2);

            entity.HasIndex(cue => cue.Name)
                .IsUnique();
        });

        builder.Entity<UserCueInventory>(entity =>
        {
            entity.HasKey(inventory => inventory.Id);

            entity.HasIndex(inventory => new { inventory.UserId, inventory.CueItemId })
                .IsUnique();

            entity.HasOne(inventory => inventory.User)
                .WithMany()
                .HasForeignKey(inventory => inventory.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(inventory => inventory.CueItem)
                .WithMany()
                .HasForeignKey(inventory => inventory.CueItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Duel>(entity =>
        {
            entity.HasKey(duel => duel.Id);

            entity.Property(duel => duel.PointsWager)
                .HasDefaultValue(100);

            entity.HasIndex(duel => duel.CreatedAtUtc);

            entity.HasOne(duel => duel.Challenger)
                .WithMany()
                .HasForeignKey(duel => duel.ChallengerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(duel => duel.Opponent)
                .WithMany()
                .HasForeignKey(duel => duel.OpponentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(duel => duel.WinnerUser)
                .WithMany()
                .HasForeignKey(duel => duel.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DuelResultSubmission>(entity =>
        {
            entity.HasKey(submission => submission.Id);

            entity.HasIndex(submission => new { submission.DuelId, submission.SubmittedByUserId, submission.RoundNumber })
                .IsUnique();

            entity.HasOne(submission => submission.Duel)
                .WithMany(duel => duel.ResultSubmissions)
                .HasForeignKey(submission => submission.DuelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(submission => submission.SubmittedByUser)
                .WithMany()
                .HasForeignKey(submission => submission.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DuelCoinFlip>(entity =>
        {
            entity.HasKey(coinFlip => coinFlip.Id);

            entity.HasIndex(coinFlip => coinFlip.DuelId)
                .IsUnique();

            entity.HasOne(coinFlip => coinFlip.Duel)
                .WithOne(duel => duel.CoinFlip)
                .HasForeignKey<DuelCoinFlip>(coinFlip => coinFlip.DuelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(coinFlip => coinFlip.FirstChooserUser)
                .WithMany()
                .HasForeignKey(coinFlip => coinFlip.FirstChooserUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(coinFlip => coinFlip.SecondChooserUser)
                .WithMany()
                .HasForeignKey(coinFlip => coinFlip.SecondChooserUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(coinFlip => coinFlip.WinnerUser)
                .WithMany()
                .HasForeignKey(coinFlip => coinFlip.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(deviceToken => deviceToken.Id);

            entity.Property(deviceToken => deviceToken.Token)
                .HasMaxLength(512)
                .IsRequired();

            entity.HasIndex(deviceToken => deviceToken.Token)
                .IsUnique();

            entity.HasOne(deviceToken => deviceToken.User)
                .WithMany(user => user.DeviceTokens)
                .HasForeignKey(deviceToken => deviceToken.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HallDayCompetition>(entity =>
        {
            entity.HasKey(competition => competition.Id);

            entity.HasIndex(competition => new { competition.PoolHallId, competition.PoolDate })
                .IsUnique();

            entity.HasIndex(competition => competition.PoolDate);

            entity.HasOne(competition => competition.PoolHall)
                .WithMany()
                .HasForeignKey(competition => competition.PoolHallId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(competition => competition.WinnerUser)
                .WithMany()
                .HasForeignKey(competition => competition.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<HallDayCompetitionEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);

            entity.HasIndex(entry => new { entry.HallDayCompetitionId, entry.UserId })
                .IsUnique();

            entity.HasOne(entry => entry.Competition)
                .WithMany(competition => competition.Entries)
                .HasForeignKey(entry => entry.HallDayCompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(entry => entry.User)
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
