using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class FridgeShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Runs",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "Households",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InviteCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InviteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MonthlyBudget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCache",
                schema: "app",
                columns: table => new
                {
                    Barcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Found = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCache", x => x.Barcode);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                schema: "app",
                columns: table => new
                {
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => new { x.HouseholdId, x.UserId });
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "app",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "app",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingItems",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Checked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingItems_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "app",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                schema: "app",
                table: "HouseholdMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Households_InviteCode",
                schema: "app",
                table: "Households",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_AddedByUserId",
                schema: "app",
                table: "Items",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_HouseholdId_Status_ExpiryDate",
                schema: "app",
                table: "Items",
                columns: new[] { "HouseholdId", "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_HouseholdId",
                schema: "app",
                table: "ShoppingItems",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdMembers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ProductCache",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ShoppingItems",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Households",
                schema: "app");

            migrationBuilder.CreateTable(
                name: "Runs",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Route = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runs_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_UserId_StartedAt",
                schema: "app",
                table: "Runs",
                columns: new[] { "UserId", "StartedAt" });
        }
    }
}
