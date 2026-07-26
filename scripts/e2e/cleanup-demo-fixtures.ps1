param(
    [string]$SqlContainer = 'fptu-sqlserver'
)

$ErrorActionPreference = 'Stop'
$userIds = @(
    'a1000000-0000-0000-0000-000000000001',
    'a1000000-0000-0000-0000-000000000002',
    'a1000000-0000-0000-0000-000000000003',
    'a1000000-0000-0000-0000-000000000004'
)
$clubId = 'b1000000-0000-0000-0000-000000000001'
$userIdSql = ($userIds | ForEach-Object { "'$_'" }) -join ','

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
        throw "SQL cleanup failed for $Database."
    }
}

Invoke-Sql notification_db @"
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SELECT COUNT(*) AS BeforeCount FROM Notifications
WHERE UserId IN ($userIdSql) OR Title LIKE 'E2E-%' OR Title LIKE 'DEMO-%';
DELETE FROM Notifications
WHERE UserId IN ($userIdSql) OR Title LIKE 'E2E-%' OR Title LIKE 'DEMO-%';
SELECT @@ROWCOUNT AS RemovedCount;
SELECT COUNT(*) AS AfterCount FROM Notifications
WHERE UserId IN ($userIdSql) OR Title LIKE 'E2E-%' OR Title LIKE 'DEMO-%';
"@

Invoke-Sql finance_db @"
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
DELETE FROM FinanceTransactions WHERE ClubId = '$clubId';
DELETE FROM BudgetProposals WHERE ClubId = '$clubId';
DELETE FROM ClubFinanceBalances WHERE ClubId = '$clubId';
"@

Invoke-Sql report_db @"
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
DELETE FROM ReportAttachments WHERE ReportId IN (SELECT Id FROM Reports WHERE ClubId = '$clubId');
DELETE FROM ReportRevisionHistories WHERE ReportId IN (SELECT Id FROM Reports WHERE ClubId = '$clubId');
DELETE FROM OutboxMessages WHERE Payload LIKE '%$clubId%' OR Payload LIKE '%E2E-%' OR Payload LIKE '%DEMO-%';
DELETE FROM Reports WHERE ClubId = '$clubId';
DELETE FROM KpiScoreHistories WHERE ClubId = '$clubId';
DELETE FROM KpiRules WHERE Name LIKE 'E2E-%' OR Name LIKE 'DEMO-%';
"@

Invoke-Sql club_db @"
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
DELETE FROM Events WHERE ClubId = '$clubId';
DELETE FROM ClubMembers WHERE ClubId = '$clubId' OR UserId IN ($userIdSql);
DELETE FROM Clubs WHERE Id = '$clubId';
"@

Invoke-Sql auth_db @"
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
DELETE FROM RefreshTokens WHERE UserId IN ($userIdSql);
DELETE FROM Users WHERE Id IN ($userIdSql);
"@

Write-Output 'Demo fixture cleanup completed.'
