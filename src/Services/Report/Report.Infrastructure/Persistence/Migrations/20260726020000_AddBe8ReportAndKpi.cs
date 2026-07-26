using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Report.Infrastructure.Persistence;

namespace Report.Infrastructure.Persistence.Migrations;

[Migration("20260726020000_AddBe8ReportAndKpi")]
[DbContext(typeof(ReportDbContext))]
public sealed class AddBe8ReportAndKpi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH('Reports', 'SemesterId') IS NULL ALTER TABLE Reports ADD SemesterId uniqueidentifier NULL;
        IF COL_LENGTH('Reports', 'RevisionNumber') IS NULL ALTER TABLE Reports ADD RevisionNumber int NOT NULL CONSTRAINT DF_Reports_RevisionNumber DEFAULT 1;
        IF COL_LENGTH('KpiRules', 'SemesterId') IS NULL ALTER TABLE KpiRules ADD SemesterId uniqueidentifier NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Reports_Semesters_SemesterId')
            EXEC('ALTER TABLE Reports ADD CONSTRAINT FK_Reports_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id)');
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_KpiRules_Semesters_SemesterId')
            EXEC('ALTER TABLE KpiRules ADD CONSTRAINT FK_KpiRules_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id)');

        IF OBJECT_ID('ReportRevisionHistories', 'U') IS NULL
        CREATE TABLE ReportRevisionHistories (
            Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL,
            RevisionNumber int NOT NULL, PreviousStatus int NOT NULL, NewStatus int NOT NULL,
            Feedback nvarchar(1000) NULL, ChangedBy uniqueidentifier NOT NULL, ChangedAt datetime2 NOT NULL,
            CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, IsActive bit NOT NULL,
            CONSTRAINT FK_ReportRevisionHistories_Reports_ReportId FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE);

        IF OBJECT_ID('KpiScoreHistories', 'U') IS NULL
        CREATE TABLE KpiScoreHistories (
            Id uniqueidentifier NOT NULL PRIMARY KEY, ClubId uniqueidentifier NOT NULL,
            SemesterId uniqueidentifier NOT NULL, RuleId uniqueidentifier NULL, Points decimal(10,2) NOT NULL,
            Reason nvarchar(500) NOT NULL, SourceType nvarchar(50) NOT NULL, SourceId uniqueidentifier NULL,
            AdjustedBy uniqueidentifier NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, IsActive bit NOT NULL,
            CONSTRAINT FK_KpiScoreHistories_Semesters_SemesterId FOREIGN KEY (SemesterId) REFERENCES Semesters(Id),
            CONSTRAINT FK_KpiScoreHistories_KpiRules_RuleId FOREIGN KEY (RuleId) REFERENCES KpiRules(Id));

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ReportRevisionHistories_ReportId_ChangedAt' AND object_id=OBJECT_ID('ReportRevisionHistories'))
            CREATE INDEX IX_ReportRevisionHistories_ReportId_ChangedAt ON ReportRevisionHistories(ReportId, ChangedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiScoreHistories_SemesterId_ClubId_CreatedAt' AND object_id=OBJECT_ID('KpiScoreHistories'))
            EXEC('CREATE INDEX IX_KpiScoreHistories_SemesterId_ClubId_CreatedAt ON KpiScoreHistories(SemesterId, ClubId, CreatedAt)');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiScoreHistories_SourceType_SourceId' AND object_id=OBJECT_ID('KpiScoreHistories'))
            EXEC('CREATE UNIQUE INDEX IX_KpiScoreHistories_SourceType_SourceId ON KpiScoreHistories(SourceType, SourceId) WHERE SourceId IS NOT NULL');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_KpiRules_SemesterId_Name' AND object_id=OBJECT_ID('KpiRules'))
            EXEC('CREATE UNIQUE INDEX IX_KpiRules_SemesterId_Name ON KpiRules(SemesterId, Name) WHERE SemesterId IS NOT NULL AND IsActive=1');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
