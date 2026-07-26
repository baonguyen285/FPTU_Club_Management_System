using Moq;
using Report.Application.Features.Reports.Commands.CreateReport;
using Report.Application.Interfaces;
using Report.Domain.Enums;
using Shared.Kernel.Exceptions;

namespace CommunicationReliability.Tests;

public sealed class ReportSubmitOwnershipTests
{
    [Fact]
    public async Task Submit_EnforcesPermissionForExactClubAndJwtActor()
    {
        var clubId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grpc = new Mock<IClubGrpcClient>();
        grpc.Setup(x => x.CheckClubExistsAsync(clubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        grpc.Setup(x => x.CanSubmitReportsAsync(clubId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var uow = new Mock<IReportUnitOfWork>();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CreateReportCommandHandler(uow.Object, grpc.Object).Handle(new CreateReportCommand
            {
                ClubId = clubId,
                CreatedBy = actorId,
                Title = "Monthly report",
                Content = "Evidence",
                Type = ReportType.Activity
            }, CancellationToken.None));

        Assert.Contains("permission", exception.Message, StringComparison.OrdinalIgnoreCase);
        grpc.Verify(x => x.CanSubmitReportsAsync(clubId, actorId, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.Reports.AddAsync(It.IsAny<Report.Domain.Entities.Report>()), Times.Never);
    }
}
