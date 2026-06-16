using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PoolTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    ShopCost = table.Column<int>(type: "integer", nullable: true),
                    AchievementCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PowerBonus = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    AccuracyBonus = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CueControlBonus = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SpinBonus = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Duels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PointsWager = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Duels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Duels_AspNetUsers_ChallengerId",
                        column: x => x.ChallengerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Duels_AspNetUsers_OpponentId",
                        column: x => x.OpponentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Duels_AspNetUsers_WinnerUserId",
                        column: x => x.WinnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlayerDailyMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalBallsPotted = table.Column<int>(type: "integer", nullable: false),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    TotalGamesWon = table.Column<int>(type: "integer", nullable: false),
                    TotalGamesLost = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerDailyMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerDailyMetrics_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvatarColorHex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    FavoriteBallNumber = table.Column<int>(type: "integer", nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    DebtPoints = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Power = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Accuracy = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CueControl = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Spin = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DuelsWon = table.Column<int>(type: "integer", nullable: false),
                    DuelsLost = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointsTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsDelta = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PoolHalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    TotalTables = table.Column<int>(type: "integer", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolHalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolHalls_AspNetUsers_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCueInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCueInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCueInventories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCueInventories_CueItems_CueItemId",
                        column: x => x.CueItemId,
                        principalTable: "CueItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DuelCoinFlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DuelId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstChooserUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondChooserUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstChooserSide = table.Column<int>(type: "integer", nullable: true),
                    SecondChooserSide = table.Column<int>(type: "integer", nullable: true),
                    ResultSide = table.Column<int>(type: "integer", nullable: true),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelCoinFlips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelCoinFlips_AspNetUsers_FirstChooserUserId",
                        column: x => x.FirstChooserUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuelCoinFlips_AspNetUsers_SecondChooserUserId",
                        column: x => x.SecondChooserUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuelCoinFlips_AspNetUsers_WinnerUserId",
                        column: x => x.WinnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DuelCoinFlips_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DuelResultSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DuelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Choice = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelResultSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelResultSubmissions_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuelResultSubmissions_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallDayCompetitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WinnerGamesWon = table.Column<int>(type: "integer", nullable: false),
                    WinnerBallsPotted = table.Column<int>(type: "integer", nullable: false),
                    ParticipantCount = table.Column<int>(type: "integer", nullable: false),
                    TotalSessions = table.Column<int>(type: "integer", nullable: false),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallDayCompetitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallDayCompetitions_AspNetUsers_WinnerUserId",
                        column: x => x.WinnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HallDayCompetitions_PoolHalls_PoolHallId",
                        column: x => x.PoolHallId,
                        principalTable: "PoolHalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoolHallRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableQuality = table.Column<int>(type: "integer", nullable: false),
                    BallsQuality = table.Column<int>(type: "integer", nullable: false),
                    CueQuality = table.Column<int>(type: "integer", nullable: false),
                    PriceValue = table.Column<int>(type: "integer", nullable: false),
                    Lighting = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolHallRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolHallRatings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoolHallRatings_PoolHalls_PoolHallId",
                        column: x => x.PoolHallId,
                        principalTable: "PoolHalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoolHallTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolHallTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolHallTables_AspNetUsers_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoolHallTables_PoolHalls_PoolHallId",
                        column: x => x.PoolHallId,
                        principalTable: "PoolHalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallDayCompetitionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HallDayCompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    GamesWon = table.Column<int>(type: "integer", nullable: false),
                    GamesLost = table.Column<int>(type: "integer", nullable: false),
                    BallsPotted = table.Column<int>(type: "integer", nullable: false),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    MinutesPlayed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallDayCompetitionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallDayCompetitionEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HallDayCompetitionEntries_HallDayCompetitions_HallDayCompet~",
                        column: x => x.HallDayCompetitionId,
                        principalTable: "HallDayCompetitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoolHallTableRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClothQuality = table.Column<int>(type: "integer", nullable: false),
                    CushionQuality = table.Column<int>(type: "integer", nullable: false),
                    Levelness = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolHallTableRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolHallTableRatings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoolHallTableRatings_PoolHallTables_PoolHallTableId",
                        column: x => x.PoolHallTableId,
                        principalTable: "PoolHallTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolHallTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EndReason = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_PoolHallTables_PoolHallTableId",
                        column: x => x.PoolHallTableId,
                        principalTable: "PoolHallTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sessions_PoolHalls_PoolHallId",
                        column: x => x.PoolHallId,
                        principalTable: "PoolHalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionReports",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BallsPotted = table.Column<int>(type: "integer", nullable: false),
                    GamesWon = table.Column<int>(type: "integer", nullable: false),
                    GamesLost = table.Column<int>(type: "integer", nullable: false),
                    SnookersEscaped = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(750)", maxLength: 750, nullable: true),
                    FlaggedForValidation = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionReports", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_SessionReports_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CueItems_Name",
                table: "CueItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId",
                table: "DeviceTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelCoinFlips_DuelId",
                table: "DuelCoinFlips",
                column: "DuelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelCoinFlips_FirstChooserUserId",
                table: "DuelCoinFlips",
                column: "FirstChooserUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelCoinFlips_SecondChooserUserId",
                table: "DuelCoinFlips",
                column: "SecondChooserUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelCoinFlips_WinnerUserId",
                table: "DuelCoinFlips",
                column: "WinnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelResultSubmissions_DuelId_SubmittedByUserId_RoundNumber",
                table: "DuelResultSubmissions",
                columns: new[] { "DuelId", "SubmittedByUserId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelResultSubmissions_SubmittedByUserId",
                table: "DuelResultSubmissions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_ChallengerId",
                table: "Duels",
                column: "ChallengerId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_CreatedAtUtc",
                table: "Duels",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_OpponentId",
                table: "Duels",
                column: "OpponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_WinnerUserId",
                table: "Duels",
                column: "WinnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HallDayCompetitionEntries_HallDayCompetitionId_UserId",
                table: "HallDayCompetitionEntries",
                columns: new[] { "HallDayCompetitionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HallDayCompetitionEntries_UserId",
                table: "HallDayCompetitionEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HallDayCompetitions_PoolDate",
                table: "HallDayCompetitions",
                column: "PoolDate");

            migrationBuilder.CreateIndex(
                name: "IX_HallDayCompetitions_PoolHallId_PoolDate",
                table: "HallDayCompetitions",
                columns: new[] { "PoolHallId", "PoolDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HallDayCompetitions_WinnerUserId",
                table: "HallDayCompetitions",
                column: "WinnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerDailyMetrics_UserId_Date",
                table: "PlayerDailyMetrics",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_UserId",
                table: "PlayerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_UserId",
                table: "PointsTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallRatings_PoolHallId_UserId_RatingDate",
                table: "PoolHallRatings",
                columns: new[] { "PoolHallId", "UserId", "RatingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallRatings_UserId",
                table: "PoolHallRatings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHalls_AddedByUserId",
                table: "PoolHalls",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallTableRatings_PoolHallTableId_UserId_RatingDate",
                table: "PoolHallTableRatings",
                columns: new[] { "PoolHallTableId", "UserId", "RatingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallTableRatings_UserId",
                table: "PoolHallTableRatings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallTables_AddedByUserId",
                table: "PoolHallTables",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHallTables_PoolHallId_TableLabel",
                table: "PoolHallTables",
                columns: new[] { "PoolHallId", "TableLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PoolHallId",
                table: "Sessions",
                column: "PoolHallId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PoolHallTableId",
                table: "Sessions",
                column: "PoolHallTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId_IsActive",
                table: "Sessions",
                columns: new[] { "UserId", "IsActive" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserCueInventories_CueItemId",
                table: "UserCueInventories",
                column: "CueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCueInventories_UserId_CueItemId",
                table: "UserCueInventories",
                columns: new[] { "UserId", "CueItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "DuelCoinFlips");

            migrationBuilder.DropTable(
                name: "DuelResultSubmissions");

            migrationBuilder.DropTable(
                name: "HallDayCompetitionEntries");

            migrationBuilder.DropTable(
                name: "PlayerDailyMetrics");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "PointsTransactions");

            migrationBuilder.DropTable(
                name: "PoolHallRatings");

            migrationBuilder.DropTable(
                name: "PoolHallTableRatings");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SessionReports");

            migrationBuilder.DropTable(
                name: "UserCueInventories");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Duels");

            migrationBuilder.DropTable(
                name: "HallDayCompetitions");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "CueItems");

            migrationBuilder.DropTable(
                name: "PoolHallTables");

            migrationBuilder.DropTable(
                name: "PoolHalls");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
