using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Report.Infrastructure.Persistence.Migrations;

public partial class AddReportOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LegacyPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RetryCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_PublishedAtUtc_NextAttemptAtUtc",
            table: "OutboxMessages",
            columns: new[] { "PublishedAtUtc", "NextAttemptAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "OutboxMessages");
}
