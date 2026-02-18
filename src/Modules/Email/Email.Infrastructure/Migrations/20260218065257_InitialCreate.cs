using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Email.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "email");

            migrationBuilder.CreateTable(
                name: "EmailRequestHeaders",
                schema: "email",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    To = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ScheduleDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRequestHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailRequestDetails",
                schema: "email",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailRequestHeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRequestDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRequestDetails_EmailRequestHeaders_EmailRequestHeaderId",
                        column: x => x.EmailRequestHeaderId,
                        principalSchema: "email",
                        principalTable: "EmailRequestHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRequestDetails_EmailRequestHeaderId",
                schema: "email",
                table: "EmailRequestDetails",
                column: "EmailRequestHeaderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailRequestDetails",
                schema: "email");

            migrationBuilder.DropTable(
                name: "EmailRequestHeaders",
                schema: "email");
        }
    }
}
