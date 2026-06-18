using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Experience",
                table: "PlayerProfiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Experience",
                table: "PlayerProfiles");
        }
    }
}
