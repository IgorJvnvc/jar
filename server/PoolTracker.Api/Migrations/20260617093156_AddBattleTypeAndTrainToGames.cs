using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleTypeAndTrainToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BattleType",
                table: "SessionGames",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PottedTrain",
                table: "SessionGames",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BattleType",
                table: "SessionGames");

            migrationBuilder.DropColumn(
                name: "PottedTrain",
                table: "SessionGames");
        }
    }
}
