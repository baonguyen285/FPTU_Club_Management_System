using Finance.Application.DTOs;
using Finance.Application.Interfaces;
using Finance.Application.Services;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Moq;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace CommunicationReliability.Tests;

public sealed class FinanceSettlementTests
{
    [Fact]
    public async Task Approval_CreatesOneDisbursementAndAllocatesBalance()
    {
        var f = new Fixture();
        var result = await f.Service.ApproveAsync(f.Proposal.Id, f.AdminId, SystemRoleNames.StudentAffairsAdmin);

        Assert.Equal(nameof(BudgetProposalStatus.Approved), result.Status);
        Assert.Equal(100, f.Balance!.AllocatedAmount);
        Assert.Equal(100, f.Balance.AvailableAmount);
        f.Repository.Verify(x => x.AddTransactionAsync(
            It.Is<FinanceTransaction>(t => t.Type == FinanceTransactionType.Disbursement && t.Amount == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateDisbursement_IsConflict()
    {
        var f = new Fixture(disbursementExists: true);
        await Assert.ThrowsAsync<ConflictException>(() =>
            f.Service.ApproveAsync(f.Proposal.Id, f.AdminId, SystemRoleNames.StudentAffairsAdmin));
    }

    [Fact]
    public async Task Treasurer_CanSettleOwnClub_AndBalanceIsUpdated()
    {
        var f = new Fixture();
        await f.Service.ApproveAsync(f.Proposal.Id, f.AdminId, SystemRoleNames.StudentAffairsAdmin);

        var result = await f.Service.SettleAsync(f.Proposal.Id,
            new SettleBudgetProposalCommand(80, "https://receipt.example/1", "Actual expense"),
            f.TreasurerId, SystemRoleNames.ClubManager);

        Assert.Equal(nameof(BudgetProposalStatus.Settled), result.Status);
        Assert.Equal(80, f.Balance!.SpentAmount);
        Assert.Equal(20, f.Balance.AvailableAmount);
    }

    [Fact]
    public async Task SettlementAboveApprovedAmount_IsRejected()
    {
        var f = new Fixture();
        await f.Service.ApproveAsync(f.Proposal.Id, f.AdminId, SystemRoleNames.StudentAffairsAdmin);
        await Assert.ThrowsAsync<InvalidDomainException>(() =>
            f.Service.SettleAsync(f.Proposal.Id,
                new SettleBudgetProposalCommand(101, "https://receipt.example/1", null),
                f.TreasurerId, SystemRoleNames.ClubManager));
    }

    [Fact]
    public async Task CrossClubTreasurer_CannotSettle()
    {
        var f = new Fixture(canManage: false);
        await f.Service.ApproveAsync(f.Proposal.Id, f.AdminId, SystemRoleNames.StudentAffairsAdmin);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            f.Service.SettleAsync(f.Proposal.Id,
                new SettleBudgetProposalCommand(50, "https://receipt.example/1", null),
                f.TreasurerId, SystemRoleNames.ClubManager));
    }

    private sealed class Fixture
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid TreasurerId { get; } = Guid.NewGuid();
        public BudgetProposal Proposal { get; }
        public ClubFinanceBalance? Balance { get; private set; }
        public Mock<IBudgetProposalRepository> Repository { get; } = new();
        public BudgetProposalService Service { get; }

        public Fixture(bool disbursementExists = false, bool canManage = true)
        {
            Proposal = new BudgetProposal(Guid.NewGuid(), null, TreasurerId, "Demo event", 100, null);
            Proposal.Submit();
            Repository.Setup(x => x.GetByIdAsync(Proposal.Id, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Proposal);
            Repository.Setup(x => x.TransactionExistsAsync(Proposal.Id, FinanceTransactionType.Disbursement, It.IsAny<CancellationToken>()))
                .ReturnsAsync(disbursementExists);
            Repository.Setup(x => x.TransactionExistsAsync(Proposal.Id, FinanceTransactionType.Expense, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            Repository.Setup(x => x.GetBalanceAsync(Proposal.ClubId, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Balance);
            Repository.Setup(x => x.AddBalanceAsync(It.IsAny<ClubFinanceBalance>(), It.IsAny<CancellationToken>()))
                .Callback<ClubFinanceBalance, CancellationToken>((b, _) => Balance = b)
                .Returns(Task.CompletedTask);
            Repository.Setup(x => x.AddTransactionAsync(It.IsAny<FinanceTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var access = new Mock<IClubAccessService>();
            access.Setup(x => x.CanManageFinanceAsync(Proposal.ClubId, TreasurerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(canManage);
            var events = new Mock<IFinanceEventPublisher>();
            events.Setup(x => x.PublishBudgetAsync(
                    It.IsAny<string>(), It.IsAny<BudgetProposal>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Service = new BudgetProposalService(Repository.Object, access.Object, events.Object);
        }
    }
}
