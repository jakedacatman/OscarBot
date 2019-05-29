using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OscarBot.Migrations
{
    public partial class Create : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Service = table.Column<string>(nullable: false),
                    Key = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Service);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "Prefixes",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildPrefix = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prefixes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Queues",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Queues", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "ModerationAction",
                columns: table => new
                {
                    UserId = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    ModeratorId = table.Column<ulong>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    ReverseAfter = table.Column<DateTime>(nullable: false),
                    Reason = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationAction", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ModerationAction_ModerationActions_GuildId",
                        column: x => x.GuildId,
                        principalTable: "ModerationActions",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skip",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<ulong>(nullable: false),
                    SongUrl = table.Column<string>(nullable: true),
                    GuildQueueGuildId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skip", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skip_Queues_GuildQueueGuildId",
                        column: x => x.GuildQueueGuildId,
                        principalTable: "Queues",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Song",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    URL = table.Column<string>(nullable: true),
                    QueuerId = table.Column<ulong>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    ChannelId = table.Column<ulong>(nullable: false),
                    Length = table.Column<string>(nullable: true),
                    Author = table.Column<string>(nullable: true),
                    Thumbnail = table.Column<string>(nullable: true),
                    GuildQueueGuildId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Song", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_Song_Queues_GuildQueueGuildId",
                        column: x => x.GuildQueueGuildId,
                        principalTable: "Queues",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAction_GuildId",
                table: "ModerationAction",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Skip_GuildQueueGuildId",
                table: "Skip",
                column: "GuildQueueGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Song_GuildQueueGuildId",
                table: "Song",
                column: "GuildQueueGuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "ModerationAction");

            migrationBuilder.DropTable(
                name: "Prefixes");

            migrationBuilder.DropTable(
                name: "Skip");

            migrationBuilder.DropTable(
                name: "Song");

            migrationBuilder.DropTable(
                name: "ModerationActions");

            migrationBuilder.DropTable(
                name: "Queues");
        }
    }
}
