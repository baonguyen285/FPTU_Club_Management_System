using AutoMapper;
using Club.Application.DTOs;
using Club.Application.Interfaces;
using Club.Application.Security;
using Club.Domain.Enums;
using MediatR;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Security;

namespace Club.Application.Features.Events.Commands.ChangeEventStatus;

public sealed class ChangeEventStatusCommandHandler : IRequestHandler<ChangeEventStatusCommand, EventDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ChangeEventStatusCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<EventDto> Handle(ChangeEventStatusCommand request, CancellationToken cancellationToken)
    {
        var clubEvent = await _unitOfWork.Clubs.GetEventByIdAsync(request.Id)
            ?? throw new NotFoundException($"Event with ID '{request.Id}' was not found.");

        if (request.TargetStatus is EventStatus.Approved or EventStatus.Rejected)
        {
            if (request.ActorRole != SystemRoleNames.StudentAffairsAdmin)
                throw new ForbiddenException("Only StudentAffairsAdmin can review an activity.");
        }
        else
        {
            await ClubAuthorization.EnsureClubLeaderOrAdminAsync(
                _unitOfWork.Clubs, clubEvent.ClubId, request.ActorId, request.ActorRole);
        }

        if (!IsValidTransition(clubEvent.Status, request.TargetStatus))
            throw new ConflictException(
                $"Event status cannot transition from {clubEvent.Status} to {request.TargetStatus}.");

        clubEvent.Status = request.TargetStatus;
        clubEvent.IsActive = request.TargetStatus != EventStatus.Cancelled;
        clubEvent.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Clubs.UpdateEvent(clubEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<EventDto>(clubEvent);
    }

    private static bool IsValidTransition(EventStatus current, EventStatus next) => current switch
    {
        EventStatus.Draft => next is EventStatus.PendingApproval or EventStatus.Cancelled,
        EventStatus.Rejected => next is EventStatus.PendingApproval or EventStatus.Cancelled,
        EventStatus.PendingApproval => next is EventStatus.Approved or EventStatus.Rejected or EventStatus.Cancelled,
        EventStatus.Approved => next is EventStatus.Completed or EventStatus.Cancelled,
        _ => false
    };
}
