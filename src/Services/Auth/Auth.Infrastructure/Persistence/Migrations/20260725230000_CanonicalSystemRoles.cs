using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Persistence.Migrations;

[Migration("20260725230000_CanonicalSystemRoles")]
public sealed class CanonicalSystemRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID('Users', 'U') IS NOT NULL
    UPDATE Users
    SET Role = 'StudentAffairsAdmin'
    WHERE Role IN ('Admin', 'Advisor');
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally irreversible: the canonical role does not retain whether a row was Admin or Advisor.
    }
}
