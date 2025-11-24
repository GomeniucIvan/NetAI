using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCallbacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_callback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processor = table.Column<string>(type: "jsonb", nullable: false),
                    event_kind = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_callback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_callback_result",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    event_callback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_callback_result", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_callback_result_event_callback_event_callback_id",
                        column: x => x.event_callback_id,
                        principalTable: "event_callback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_callback_created_at",
                table: "event_callback",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_event_callback_result_conversation_id",
                table: "event_callback_result",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_callback_result_created_at",
                table: "event_callback_result",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_event_callback_result_event_callback_id",
                table: "event_callback_result",
                column: "event_callback_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_callback_result_event_id",
                table: "event_callback_result",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_callback_result");

            migrationBuilder.DropTable(
                name: "event_callback");
        }
    }
}
