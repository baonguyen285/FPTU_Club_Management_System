using Club.Application.DTOs;
using Club.Domain.Enums;
using MediatR;

namespace Club.Application.Features.Events.Commands.ChangeEventStatus;

public sealed class ChangeEventStatusCommand : IRequest<EventDto>
{
    public Guid Id { get; init; }
    public EventStatus TargetStatus { get; init; }
    public Guid ActorId { get; init; }
    public string ActorRole { get; init; } = string.Empty;
}
