using System.Threading;
using System.Threading.Tasks;
using Club.Application.Interfaces;
using Club.Infrastructure.Persistence;

namespace Club.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ClubDbContext _context;
        private IClubRepository? _clubRepository;

        public UnitOfWork(ClubDbContext context)
        {
            _context = context;
        }

        public IClubRepository Clubs => _clubRepository ??= new ClubRepository(_context);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
