using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Club.Application.Interfaces;
using Club.Domain.Entities;
using Club.Infrastructure.Persistence;

namespace Club.Infrastructure.Repositories
{
    public class ClubRepository : IClubRepository
    {
        private readonly ClubDbContext _context;

        public ClubRepository(ClubDbContext context)
        {
            _context = context;
        }

        public async Task<Domain.Entities.Club?> GetByIdAsync(Guid id)
        {
            return await _context.Clubs
                .Include(c => c.Members)
                .Include(c => c.Events)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Domain.Entities.Club>> GetAllAsync()
        {
            return await _context.Clubs.ToListAsync();
        }

        public async Task AddAsync(Domain.Entities.Club club)
        {
            await _context.Clubs.AddAsync(club);
        }

        public void Update(Domain.Entities.Club club)
        {
            _context.Clubs.Update(club);
        }

        public void Delete(Domain.Entities.Club club)
        {
            _context.Clubs.Remove(club);
        }

        public async Task<ClubMember?> GetMemberAsync(Guid clubId, Guid userId)
        {
            return await _context.ClubMembers
                .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == userId);
        }

        public async Task<IEnumerable<ClubMember>> GetMembersByClubAsync(Guid clubId)
        {
            return await _context.ClubMembers
                .Where(m => m.ClubId == clubId)
                .ToListAsync();
        }

        public async Task AddMemberAsync(ClubMember member)
        {
            await _context.ClubMembers.AddAsync(member);
        }

        public void UpdateMember(ClubMember member)
        {
            _context.ClubMembers.Update(member);
        }

        public void DeleteMember(ClubMember member)
        {
            _context.ClubMembers.Remove(member);
        }

        public async Task<Event?> GetEventByIdAsync(Guid eventId)
        {
            return await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        }

        public async Task<IEnumerable<Event>> GetEventsByClubAsync(Guid clubId)
        {
            return await _context.Events
                .Where(e => e.ClubId == clubId)
                .ToListAsync();
        }

        public async Task AddEventAsync(Event clubEvent)
        {
            await _context.Events.AddAsync(clubEvent);
        }

        public void UpdateEvent(Event clubEvent)
        {
            _context.Events.Update(clubEvent);
        }

        public void DeleteEvent(Event clubEvent)
        {
            _context.Events.Remove(clubEvent);
        }
    }
}
