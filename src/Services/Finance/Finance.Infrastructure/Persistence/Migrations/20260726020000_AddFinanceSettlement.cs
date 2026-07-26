using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Finance.Infrastructure.Persistence;

namespace Finance.Infrastructure.Persistence.Migrations;

[Migration("20260726020000_AddFinanceSettlement")]
[DbContext(typeof(FinanceDbContext))]
public sealed class AddFinanceSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH('BudgetProposals','ActualAmount') IS NULL ALTER TABLE BudgetProposals ADD ActualAmount decimal(18,2) NULL;
        IF COL_LENGTH('BudgetProposals','ReceiptUrl') IS NULL ALTER TABLE BudgetProposals ADD ReceiptUrl nvarchar(1000) NULL;
        IF COL_LENGTH('BudgetProposals','SettlementDescription') IS NULL ALTER TABLE BudgetProposals ADD SettlementDescription nvarchar(500) NULL;
        IF COL_LENGTH('BudgetProposals','SettledBy') IS NULL ALTER TABLE BudgetProposals ADD SettledBy uniqueidentifier NULL;
        IF COL_LENGTH('BudgetProposals','SettledAt') IS NULL ALTER TABLE BudgetProposals ADD SettledAt datetime2 NULL;
        IF COL_LENGTH('FinanceTransactions','CreatedBy') IS NULL ALTER TABLE FinanceTransactions ADD CreatedBy uniqueidentifier NOT NULL CONSTRAINT DF_FinanceTransactions_CreatedBy DEFAULT '00000000-0000-0000-0000-000000000000';
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FinanceTransactions_ReferenceId_Type' AND object_id=OBJECT_ID('FinanceTransactions'))
            EXEC('CREATE UNIQUE INDEX IX_FinanceTransactions_ReferenceId_Type ON FinanceTransactions(ReferenceId, Type) WHERE ReferenceId IS NOT NULL AND Type IN (''Disbursement'',''Expense'')');
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
