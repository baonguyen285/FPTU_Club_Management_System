-- Additive local/runtime migration. Program.cs executes the same idempotent DDL
-- because this service historically used EnsureCreated rather than EF migrations.
IF OBJECT_ID(N'[ClubApplications]', N'U') IS NULL
BEGIN
    CREATE TABLE [ClubApplications] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [ApplicantUserId] uniqueidentifier NOT NULL,
        [ProposedClubName] nvarchar(150) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Objectives] nvarchar(4000) NOT NULL,
        [EvidenceUrlsJson] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [ReviewFeedback] nvarchar(2000) NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [CreatedClubId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsActive] bit NOT NULL
    );
    CREATE INDEX [IX_ClubApplications_Status] ON [ClubApplications] ([Status]);
    CREATE UNIQUE INDEX [IX_ClubApplications_CreatedClubId]
        ON [ClubApplications] ([CreatedClubId]) WHERE [CreatedClubId] IS NOT NULL;
END
