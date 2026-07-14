using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Club.Domain.Entities;

namespace Club.Application.Interfaces
{
    public interface IClubRepository
    {
        Task<Domain.Entities.Club?> GetByIdAsync(Guid id);
        Task<IEnumerable<Domain.Entities.Club>> GetAllAsync();
        Task AddAsync(Domain.Entities.Club club);
        void Update(Domain.Entities.Club club);
        void Delete(Domain.Entities.Club club);

        Task<ClubMember?> GetMemberAsync(Guid clubId, Guid userId);
        Task<IEnumerable<ClubMember>> GetMembersByClubAsync(Guid clubId);
        Task<IEnumerable<ClubMember>> GetMembersByUserAsync(Guid userId);
        Task AddMemberAsync(ClubMember member);
        void UpdateMember(ClubMember member);
        void DeleteMember(ClubMember member);

        Task<Event?> GetEventByIdAsync(Guid eventId);
        Task<IEnumerable<Event>> GetEventsByClubAsync(Guid clubId);
        Task AddEventAsync(Event clubEvent);
        void UpdateEvent(Event clubEvent);
        void DeleteEvent(Event clubEvent);
    }
}
