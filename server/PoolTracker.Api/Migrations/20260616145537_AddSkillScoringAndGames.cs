using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillScoringAndGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccuracyDelta",
                table: "SessionReports",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BallsPottedOnBreak",
                table: "SessionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CueControlDelta",
                table: "SessionReports",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GamesBroken",
                table: "SessionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoldenBreaks",
                table: "SessionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PowerDelta",
                table: "SessionReports",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SnookersFaced",
                table: "SessionReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SpinDelta",
                table: "SessionReports",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SessionGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    GameType = table.Column<int>(type: "integer", nullable: false),
                    BrokeThisRack = table.Column<bool>(type: "boolean", nullable: false),
                    BreakPots = table.Column<int>(type: "integer", nullable: false),
                    BallsPotted = table.Column<int>(type: "integer", nullable: false),
                    SnookersFaced = table.Column<int>(type: "integer", nullable: false),
                    SnookersEscaped = table.Column<int>(type: "integer", nullable: false),
                    Won = table.Column<bool>(type: "boolean", nullable: false),
                    GoldenBreak = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionGames_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionGames_SessionId_Sequence",
                table: "SessionGames",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionGames");

            migrationBuilder.DropColumn(
                name: "AccuracyDelta",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "BallsPottedOnBreak",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "CueControlDelta",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "GamesBroken",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "GoldenBreaks",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "PowerDelta",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "SnookersFaced",
                table: "SessionReports");

            migrationBuilder.DropColumn(
                name: "SpinDelta",
                table: "SessionReports");
        }
    }
}
