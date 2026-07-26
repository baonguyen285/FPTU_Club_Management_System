param(
    [string]$SqlContainer = 'fptu-sqlserver'
)

$ErrorActionPreference = 'Stop'

$ids = @{
    Admin = 'a1000000-0000-0000-0000-000000000001'
    Leader = 'a1000000-0000-0000-0000-000000000002'
    Treasurer = 'a1000000-0000-0000-0000-000000000003'
    Student = 'a1000000-0000-0000-0000-000000000004'
    Club = 'b1000000-0000-0000-0000-000000000001'
    LeaderMembership = 'c1000000-0000-0000-0000-000000000002'
    TreasurerMembership = 'c1000000-0000-0000-0000-000000000003'
}

$container = docker inspect $SqlContainer | ConvertFrom-Json
$passwordEntry = $container[0].Config.Env |
    Where-Object { $_ -like 'SA_PASSWORD=*' } |
    Select-Object -First 1
if (-not $passwordEntry) {
    throw "SA_PASSWORD is not available in $SqlContainer."
}
$sqlPassword = $passwordEntry.Substring('SA_PASSWORD='.Length)

function Invoke-Sql {
    param([string]$Database, [string]$Query)
    docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $sqlPassword -C -b -d $Database -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "SQL fixture operation failed for $Database."
    }
}

# The password hash is copied from the existing local demo admin. No password or
# token is embedded in this script. Supply the matching demo password at runtime
# when executing API tests.
$userSql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @PasswordHash nvarchar(max) =
    (SELECT PasswordHash FROM Users WHERE Email = 'admin@fpt.edu.vn');
IF @PasswordHash IS NULL THROW 50001, 'Reference demo password hash not found.', 1;

MERGE Users AS target
USING (VALUES
    ('$($ids.Admin)', 'demo.admin@fpt.edu.vn', 'DEMO Admin', 'StudentAffairsAdmin'),
    ('$($ids.Leader)', 'demo.leader@fpt.edu.vn', 'DEMO Club Leader', 'ClubManager'),
    ('$($ids.Treasurer)', 'demo.treasurer@fpt.edu.vn', 'DEMO Treasurer', 'ClubManager'),
    ('$($ids.Student)', 'demo.student@fpt.edu.vn', 'DEMO Student', 'Student')
) AS source(Id, Email, FullName, Role)
ON target.Id = CONVERT(uniqueidentifier, source.Id)
WHEN MATCHED THEN UPDATE SET
    Email = source.Email,
    FullName = source.FullName,
    Role = source.Role,
    PasswordHash = @PasswordHash,
    IsActive = 1,
    IsEmailVerified = 1,
    EmailVerificationCode = NULL,
    EmailVerificationCodeExpiresAt = NULL,
    ResetPasswordCode = NULL,
    ResetPasswordCodeExpiresAt = NULL
WHEN NOT MATCHED THEN INSERT
    (Id, Email, PasswordHash, FullName, Role, IsActive, IsEmailVerified, CreatedAt)
VALUES
    (CONVERT(uniqueidentifier, source.Id), source.Email, @PasswordHash,
     source.FullName, source.Role, 1, 1, SYSUTCDATETIME());
COMMIT;
"@

$clubSql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
MERGE Clubs AS target
USING (VALUES
    ('$($ids.Club)', 'DEMO-E2E Club', 'Disposable club for canonical-role E2E verification.')
) AS source(Id, Name, Description)
ON target.Id = CONVERT(uniqueidentifier, source.Id)
WHEN MATCHED THEN UPDATE SET
    Name = source.Name,
    Description = source.Description,
    AdvisorId = '$($ids.Admin)',
    Status = 1,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (Id, Name, Description, AdvisorId, Status, CreatedAt, IsActive)
VALUES
    (CONVERT(uniqueidentifier, source.Id), source.Name, source.Description,
     '$($ids.Admin)', 1, SYSUTCDATETIME(), 1);

MERGE ClubMembers AS target
USING (VALUES
    ('$($ids.LeaderMembership)', '$($ids.Leader)', 2),
    ('$($ids.TreasurerMembership)', '$($ids.Treasurer)', 3)
) AS source(Id, UserId, Role)
ON target.Id = CONVERT(uniqueidentifier, source.Id)
WHEN MATCHED THEN UPDATE SET
    ClubId = '$($ids.Club)',
    UserId = CONVERT(uniqueidentifier, source.UserId),
    Role = source.Role,
    Status = 1,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (Id, ClubId, UserId, Role, Status, JoinedAt, CreatedAt, IsActive)
VALUES
    (CONVERT(uniqueidentifier, source.Id), '$($ids.Club)',
     CONVERT(uniqueidentifier, source.UserId), source.Role, 1,
     SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
COMMIT;
"@

Invoke-Sql -Database auth_db -Query $userSql
Invoke-Sql -Database club_db -Query $clubSql

[pscustomobject]@{
    AdminId = $ids.Admin
    AdminEmail = 'demo.admin@fpt.edu.vn'
    LeaderId = $ids.Leader
    LeaderEmail = 'demo.leader@fpt.edu.vn'
    TreasurerId = $ids.Treasurer
    TreasurerEmail = 'demo.treasurer@fpt.edu.vn'
    StudentId = $ids.Student
    StudentEmail = 'demo.student@fpt.edu.vn'
    ClubId = $ids.Club
    ClubName = 'DEMO-E2E Club'
} | Format-List
