using Club.Domain.Entities;

namespace Club.Application.Interfaces;

public interface IClubEventPublisher
{
    Task PublishActivityCreatedAsync(Event clubEvent, CancellationToken cancellationToken);
    Task PublishClubApplicationReviewedAsync(
        ClubApplication application, CancellationToken cancellationToken);
}
