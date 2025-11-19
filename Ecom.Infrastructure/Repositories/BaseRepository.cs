using Ecom.Domain.comman;
using Ecom.Infrastructure.Data;
using Ecom.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq.Expressions;

namespace Ecom.Infrastructure.Repositories
{
    public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
    {
        protected readonly EcomDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly IMemoryCache _cache;
        protected readonly string _entityTypeName;
        
        // Cache configuration
        private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromMinutes(520);
        private static readonly TimeSpan SlidingCacheExpiration = TimeSpan.FromMinutes(320);

        public BaseRepository(EcomDbContext context, IMemoryCache cache)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _cache = cache;
            _entityTypeName = typeof(T).Name;
        }

        // Cache key helpers
        private string GetCacheKey(string suffix) => $"{_entityTypeName}_{suffix}";
        private string GetByIdCacheKey(int id) => GetCacheKey($"Id_{id}");
        private string GetAllCacheKey() => GetCacheKey("All");
        private string GetAllActiveCacheKey() => GetCacheKey("AllActive");
        
        private void InvalidateCache(params string[] keys)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
            // Also remove collection cache keys
            _cache.Remove(GetAllCacheKey());
            _cache.Remove(GetAllActiveCacheKey());
        }

        private MemoryCacheEntryOptions GetCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DefaultCacheExpiration,
                SlidingExpiration = SlidingCacheExpiration,
                Priority = CacheItemPriority.Normal
            };
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            var cacheKey = GetByIdCacheKey(id);
            
            if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
            {
                return cachedEntity;
            }

            var entity = await _dbSet.FindAsync(id);
            
            if (entity != null)
            {
                _cache.Set(cacheKey, entity, GetCacheOptions());
            }

            return entity;
        }

        public virtual async Task<T?> GetActiveByIdAsync(int id)
        {
            var cacheKey = GetByIdCacheKey(id);
            
            if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
            {
                if (cachedEntity != null && cachedEntity.IsActive && !cachedEntity.IsDeleted)
                {
                    return cachedEntity;
                }
            }

            var entity = await _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (entity != null && entity.IsActive)
            {
                var options = new MemoryCacheEntryOptions()
                    .SetSize(1) // ãåã ÌÏÇð ãÚ SizeLimit
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                _cache.Set(cacheKey, entity, options);
            }


            return entity;
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            var cacheKey = GetAllCacheKey();
            
            if (_cache.TryGetValue(cacheKey, out List<T>? cachedList))
            {
                return cachedList ?? new List<T>();
            }

            var entities = await _dbSet.ToListAsync();
            
            if (entities.Any())
            {
                _cache.Set(cacheKey, entities, GetCacheOptions());
            }

            return entities;
        }

        public virtual async Task<List<T>> GetAllActiveAsync()
        {
            var cacheKey = GetAllActiveCacheKey();
            
            if (_cache.TryGetValue(cacheKey, out List<T>? cachedList))
            {
                return cachedList ?? new List<T>();
            }

            var entities = await _dbSet.Where(x => x.IsActive && !x.IsDeleted).ToListAsync();
            
            if (entities.Any())
            {
                _cache.Set(cacheKey, entities, GetCacheOptions());
            }

            return entities;
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            // For complex queries with predicates, we don't cache as they're too varied
            // Only cache simple, frequently used queries
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindActiveAsync(Expression<Func<T, bool>> predicate)
        {
            // For complex queries with predicates, we don't cache as they're too varied
            return await _dbSet.Where(x => x.IsActive && !x.IsDeleted).Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            // For complex queries with predicates, we don't cache as they're too varied
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<T?> FirstOrDefaultActiveAsync(Expression<Func<T, bool>> predicate)
        {
            // For complex queries with predicates, we don't cache as they're too varied
            return await _dbSet.Where(x => x.IsActive && !x.IsDeleted).FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            if (predicate == null)
                return await _dbSet.CountAsync();
            
            return await _dbSet.CountAsync(predicate);
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            InvalidateCache(GetByIdCacheKey(entity.Id));
            return entity;
        }

        public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            var entityIds = entities.Select(e => GetByIdCacheKey(e.Id)).ToArray();
            InvalidateCache(entityIds);
            return entities;
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
            InvalidateCache(GetByIdCacheKey(entity.Id));
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
            var entityIds = entities.Select(e => GetByIdCacheKey(e.Id)).ToArray();
            InvalidateCache(entityIds);
        }

        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
            InvalidateCache(GetByIdCacheKey(entity.Id));
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            var entityIds = entities.Select(e => GetByIdCacheKey(e.Id)).ToArray();
            InvalidateCache(entityIds);
        }

        public virtual void SoftDelete(T entity)
        {
            entity.IsDeleted = true;
            Update(entity);
            // Cache invalidation is handled by Update method
        }

        public virtual void SoftDeleteRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsDeleted = true;
            }
            UpdateRange(entities);
            // Cache invalidation is handled by UpdateRange method
        }

        public virtual void Activate(T entity)
        {
            entity.IsActive = true;
            Update(entity);
            // Cache invalidation is handled by Update method
        }

        public virtual void Deactivate(T entity)
        {
            entity.IsActive = false;
            Update(entity);
            // Cache invalidation is handled by Update method
        }

        public virtual void ActivateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsActive = true;
            }
            UpdateRange(entities);
            // Cache invalidation is handled by UpdateRange method
        }

        public virtual void DeactivateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsActive = false;
            }
            UpdateRange(entities);
            // Cache invalidation is handled by UpdateRange method
        }

        public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            var query = _dbSet.AsQueryable();

            if (predicate != null)
                query = query.Where(predicate);

            var totalCount = await query.CountAsync();

            if (orderBy != null)
                query = orderBy(query);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}

