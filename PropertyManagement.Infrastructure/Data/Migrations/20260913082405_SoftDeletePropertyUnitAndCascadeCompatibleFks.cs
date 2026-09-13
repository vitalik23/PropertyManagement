using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeletePropertyUnitAndCascadeCompatibleFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leases_RentalApplications_RentalApplicationId",
                table: "Leases");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalApplications_AspNetUsers_ApplicantUserId",
                table: "RentalApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalApplications_Units_UnitId",
                table: "RentalApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_UnitTypes_UnitTypeId",
                table: "Units");

            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_Leases_RentalApplications_RentalApplicationId",
                table: "Leases",
                column: "RentalApplicationId",
                principalTable: "RentalApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalApplications_AspNetUsers_ApplicantUserId",
                table: "RentalApplications",
                column: "ApplicantUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalApplications_Units_UnitId",
                table: "RentalApplications",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_UnitTypes_UnitTypeId",
                table: "Units",
                column: "UnitTypeId",
                principalTable: "UnitTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leases_RentalApplications_RentalApplicationId",
                table: "Leases");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalApplications_AspNetUsers_ApplicantUserId",
                table: "RentalApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_RentalApplications_Units_UnitId",
                table: "RentalApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_UnitTypes_UnitTypeId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsRemoved",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsRemoved",
                table: "Properties");

            migrationBuilder.AddForeignKey(
                name: "FK_Leases_RentalApplications_RentalApplicationId",
                table: "Leases",
                column: "RentalApplicationId",
                principalTable: "RentalApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalApplications_AspNetUsers_ApplicantUserId",
                table: "RentalApplications",
                column: "ApplicantUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RentalApplications_Units_UnitId",
                table: "RentalApplications",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_UnitTypes_UnitTypeId",
                table: "Units",
                column: "UnitTypeId",
                principalTable: "UnitTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
