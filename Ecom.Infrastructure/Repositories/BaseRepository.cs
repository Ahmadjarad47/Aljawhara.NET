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
        private readonly string _cachePrefix;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);

        public BaseRepository(EcomDbContext context, IMemoryCache cache)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _cache = cache;
            _cachePrefix = $"repo:{typeof(T).Name}";
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            var cacheKey = GetByIdCacheKey(id);

            if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
            {
                return cachedEntity;
            }

            var entity = await _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

            if (entity != null)
            {
                _cache.Set(cacheKey, entity, GetCacheEntryOptions());
            }

            return entity;
        }

        public virtual async Task<T?> GetActiveByIdAsync(int id)
        {
            var cacheKey = GetActiveByIdCacheKey(id);

            if (_cache.TryGetValue(cacheKey, out T? cachedEntity))
            {
                return cachedEntity;
            }

            var entity = await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted);

            if (entity != null)
            {
                _cache.Set(cacheKey, entity, GetCacheEntryOptions());
            }

            return entity;
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            var cacheKey = GetAllCacheKey();

            if (_cache.TryGetValue(cacheKey, out List<T>? cachedList))
            {
                return cachedList;
            }

            var items = await _dbSet.AsNoTracking().ToListAsync();

            _cache.Set(cacheKey, items, GetCacheEntryOptions(items.Count));

            return items;
        }

        public virtual async Task<List<T>> GetAllActiveAsync()
        {
            var cacheKey = GetAllActiveCacheKey();

            if (_cache.TryGetValue(cacheKey, out List<T>? cachedList))
            {
                return cachedList;
            }

            var items = await _dbSet
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .ToListAsync();

            _cache.Set(cacheKey, items, GetCacheEntryOptions(items.Count));

            return items;
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
            InvalidateCollectionCache();
            return entity;
        }

        public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            InvalidateCollectionCache();
            return entities;
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
            InvalidateCacheForEntity(entity);
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
            InvalidateCollectionCache();
        }

        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
            InvalidateCacheForEntity(entity);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
            InvalidateCollectionCache();
        }

        public virtual void SoftDelete(T entity)
        {
            entity.IsDeleted = true;
            Update(entity);
        }

        public virtual void SoftDeleteRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsDeleted = true;
            }
            UpdateRange(entities);
        }

        public virtual void Activate(T entity)
        {
            entity.IsActive = true;
            Update(entity);
        }

        public virtual void Deactivate(T entity)
        {
            entity.IsActive = false;
            Update(entity);
        }

        public virtual void ActivateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsActive = true;
            }
            UpdateRange(entities);
        }

        public virtual void DeactivateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                entity.IsActive = false;
            }
            UpdateRange(entities);
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

        #region Caching helpers

        private string GetAllCacheKey() => $"{_cachePrefix}:all";

        private string GetAllActiveCacheKey() => $"{_cachePrefix}:all:active";

        private string GetByIdCacheKey(int id) => $"{_cachePrefix}:id:{id}";

        private string GetActiveByIdCacheKey(int id) => $"{_cachePrefix}:id:{id}:active";

        private static MemoryCacheEntryOptions GetCacheEntryOptions(int size = 1)
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = size
            };
        }

        /// <summary>
        /// Invalidate cached collections (GetAll / GetAllActive) for this entity type.
        /// Called after add / bulk updates / bulk deletes where we don't track specific IDs.
        /// </summary>
        protected virtual void InvalidateCollectionCache()
        {
            _cache.Remove(GetAllCacheKey());
            _cache.Remove(GetAllActiveCacheKey());
        }

        /// <summary>
        /// Invalidate cache entries related to a specific entity plus the collections.
        /// </summary>
        protected virtual void InvalidateCacheForEntity(T entity)
        {
            _cache.Remove(GetByIdCacheKey(entity.Id));
            _cache.Remove(GetActiveByIdCacheKey(entity.Id));
            InvalidateCollectionCache();
        }

        #endregion
    }
}

