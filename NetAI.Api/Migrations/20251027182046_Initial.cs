using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelectedRepository = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SelectedBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GitProvider = table.Column<string>(type: "text", nullable: true),
                    GitProviderRaw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Trigger = table.Column<string>(type: "text", nullable: true),
                    PullRequestNumbers = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LlmModel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AccumulatedCost = table.Column<double>(type: "double precision", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    SandboxId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConversationVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RuntimeStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RuntimeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SessionApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    VscodeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationStartTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FailureDetail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AppConversationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SandboxId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AgentServerUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SandboxSessionApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SandboxWorkspacePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SandboxVscodeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConversationStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RuntimeStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BackendError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Request = table.Column<string>(type: "text", nullable: true, defaultValue: "{}"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStartTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SandboxSpecs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Command = table.Column<string>(type: "text", nullable: true),
                    InitialEnv = table.Column<string>(type: "text", nullable: true, defaultValue: "{}"),
                    WorkingDir = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, defaultValue: "/home/openhands/workspace"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxSpecs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationEvents",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PayloadJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationEvents", x => new { x.ConversationMetadataRecordId, x.EventId });
                    table.ForeignKey(
                        name: "FK_ConversationEvents_ConversationMetadata_ConversationMetadat~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationFeedbackEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FeedbackJson = table.Column<string>(type: "text", nullable: true),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFeedbackEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationFeedbackEntries_ConversationMetadata_Conversati~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationFiles_ConversationMetadata_ConversationMetadata~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationGitDiffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Original = table.Column<string>(type: "text", nullable: true),
                    Modified = table.Column<string>(type: "text", nullable: true),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationGitDiffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationGitDiffs_ConversationMetadata_ConversationMetad~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMicroagents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    TriggersJson = table.Column<string>(type: "text", nullable: true),
                    InputsJson = table.Column<string>(type: "text", nullable: true),
                    ToolsJson = table.Column<string>(type: "text", nullable: true),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMicroagents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMicroagents_ConversationMetadata_ConversationMe~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationRememberPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationRememberPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationRememberPrompts_ConversationMetadata_Conversati~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationRuntimeInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SessionApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RuntimeStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VscodeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ConversationMetadataRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationRuntimeInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationRuntimeInstances_ConversationMetadata_Conversat~",
                        column: x => x.ConversationMetadataRecordId,
                        principalTable: "ConversationMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sandboxes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SandboxSpecId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SessionApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExposedUrls = table.Column<string>(type: "text", nullable: true, defaultValue: "[]"),
                    RuntimeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RuntimeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    WorkspacePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RuntimeHosts = table.Column<string>(type: "text", nullable: true, defaultValue: "[]"),
                    RuntimeState = table.Column<string>(type: "text", nullable: true, defaultValue: "{}"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sandboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sandboxes_SandboxSpecs_SandboxSpecId",
                        column: x => x.SandboxSpecId,
                        principalTable: "SandboxSpecs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationRuntimeHosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConversationRuntimeInstanceRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationRuntimeHosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationRuntimeHosts_ConversationRuntimeInstances_Conve~",
                        column: x => x.ConversationRuntimeInstanceRecordId,
                        principalTable: "ConversationRuntimeInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationRuntimeProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConversationRuntimeInstanceRecordId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationRuntimeProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationRuntimeProviders_ConversationRuntimeInstances_C~",
                        column: x => x.ConversationRuntimeInstanceRecordId,
                        principalTable: "ConversationRuntimeInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFeedbackEntries_ConversationMetadataRecordId",
                table: "ConversationFeedbackEntries",
                column: "ConversationMetadataRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFiles_ConversationMetadataRecordId_Path",
                table: "ConversationFiles",
                columns: new[] { "ConversationMetadataRecordId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationGitDiffs_ConversationMetadataRecordId_Path",
                table: "ConversationGitDiffs",
                columns: new[] { "ConversationMetadataRecordId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetadata_ConversationId",
                table: "ConversationMetadata",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMicroagents_ConversationMetadataRecordId_Name",
                table: "ConversationMicroagents",
                columns: new[] { "ConversationMetadataRecordId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationRememberPrompts_ConversationMetadataRecordId_Ev~",
                table: "ConversationRememberPrompts",
                columns: new[] { "ConversationMetadataRecordId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationRuntimeHosts_ConversationRuntimeInstanceRecordI~",
                table: "ConversationRuntimeHosts",
                columns: new[] { "ConversationRuntimeInstanceRecordId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationRuntimeInstances_ConversationMetadataRecordId",
                table: "ConversationRuntimeInstances",
                column: "ConversationMetadataRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationRuntimeProviders_ConversationRuntimeInstanceRec~",
                table: "ConversationRuntimeProviders",
                columns: new[] { "ConversationRuntimeInstanceRecordId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStartTasks_AppConversationId",
                table: "ConversationStartTasks",
                column: "AppConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStartTasks_CreatedAtUtc",
                table: "ConversationStartTasks",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sandboxes_SandboxSpecId",
                table: "Sandboxes",
                column: "SandboxSpecId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationEvents");

            migrationBuilder.DropTable(
                name: "ConversationFeedbackEntries");

            migrationBuilder.DropTable(
                name: "ConversationFiles");

            migrationBuilder.DropTable(
                name: "ConversationGitDiffs");

            migrationBuilder.DropTable(
                name: "ConversationMicroagents");

            migrationBuilder.DropTable(
                name: "ConversationRememberPrompts");

            migrationBuilder.DropTable(
                name: "ConversationRuntimeHosts");

            migrationBuilder.DropTable(
                name: "ConversationRuntimeProviders");

            migrationBuilder.DropTable(
                name: "ConversationStartTasks");

            migrationBuilder.DropTable(
                name: "Sandboxes");

            migrationBuilder.DropTable(
                name: "ConversationRuntimeInstances");

            migrationBuilder.DropTable(
                name: "SandboxSpecs");

            migrationBuilder.DropTable(
                name: "ConversationMetadata");
        }
    }
}
