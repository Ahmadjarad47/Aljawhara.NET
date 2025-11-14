using Ecom.Domain.Entity;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Ecom.Infrastructure.Repositories
{
    public class CarouselRepository : BaseRepository<Carousel>, ICarouselRepository
    {
        public CarouselRepository(EcomDbContext context, IMemoryCache cache) : base(context, cache)
        {
        }
    }
}

