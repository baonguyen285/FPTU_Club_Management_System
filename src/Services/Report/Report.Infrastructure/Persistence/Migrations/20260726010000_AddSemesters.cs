using Microsoft.EntityFrameworkCore.Migrations;

namespace Report.Infrastructure.Persistence.Migrations;

[Migration("20260726010000_AddSemesters")]
public sealed class AddSemesters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        IF OBJECT_ID('Semesters', 'U') IS NULL
        BEGIN
            CREATE TABLE Semesters (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                Code nvarchar(30) NOT NULL,
                Name nvarchar(150) NOT NULL,
                StartDate datetime2 NOT NULL,
                EndDate datetime2 NOT NULL,
                Status int NOT NULL,
                CreatedAt datetime2 NOT NULL,
                UpdatedAt datetime2 NULL,
                IsActive bit NOT NULL
            );
        END
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Semesters_Code' AND object_id = OBJECT_ID('Semesters'))
            EXEC('CREATE UNIQUE INDEX IX_Semesters_Code ON Semesters(Code)');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Semesters_Status_Active' AND object_id = OBJECT_ID('Semesters'))
            EXEC('CREATE UNIQUE INDEX IX_Semesters_Status_Active ON Semesters(Status) WHERE Status = 1');
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Semesters");
    }
}
