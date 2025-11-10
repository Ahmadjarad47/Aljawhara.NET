using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class VisitorRepository : BaseRepository<Visitor>, IVisitorRepository
    {
        public VisitorRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }
    }
}


