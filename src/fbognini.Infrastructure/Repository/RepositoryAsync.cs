using EFCore.BulkExtensions;
using fbognini.Core.Exceptions;
using fbognini.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Snickler.EFCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using fbognini.Core.Domain.Query;
using fbognini.Core.Domain.Query.Pagination;
using fbognini.Core.Domain;
using Microsoft.EntityFrameworkCore.Query;

namespace fbognini.Infrastructure.Repository
{
    public class RepositoryAsync<TContext> : IRepositoryAsync, IDisposable
        where TContext : DbContext, IBaseDbContext
    {
        private readonly TContext context;
        private readonly ILogger<RepositoryAsync<TContext>> logger;

        protected readonly Hashtable repositorys = new();

        public RepositoryAsync(IDbContextFactory<TContext> dbContextFactory, ILogger<RepositoryAsync<TContext>> logger)
        {
            this.context = dbContextFactory.CreateDbContext();
            this.logger = logger;
        }

        public RepositoryAsync(TContext context, ILogger<RepositoryAsync<TContext>> logger)
        {
            this.context = context;
            this.logger = logger;
        }


        public string? GetConnectionString() => context.ConnectionString ?? context.Database.GetConnectionString();

#if NET7_0_OR_GREATER
        public async Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default)
        {
            return await context.Database.ExecuteSqlAsync(sql, cancellationToken);
        }
#endif

        public IQueryable<T> GetQueryable<T, TPK>(SelectArgs<T, TPK>? args = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            return GetPrivateQueryable(args);
        }

        public IQueryable<T> GetQueryableById<T, TPK>(TPK id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            return GetPrivateQueryableById(id, args);
        }

        public IQueryable<T> GetQueryableById<T>(int id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<int>
        {
            return GetPrivateQueryableById(id, args);
        }

        public IQueryable<T> GetQueryableById<T>(long id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<long>
        {
            return GetPrivateQueryableById(id, args);
        }

        public IQueryable<T> GetQueryableById<T>(string id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<string>
        {
            return GetPrivateQueryableById(id, args);
        }

        public IQueryable<T> GetQueryableById<T>(Guid id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<Guid>
        {
            return GetPrivateQueryableById(id, args);
        }

        public IQueryable<T> GetQueryableByName<T>(string name, SelectArgs<T>? args = null)
            where T : class, IEntity, IHaveName
        {
            return GetPrivateQueryableByName(name, args);
        }

        public IQueryable<T> GetQueryableBySlug<T>(string slug, SelectArgs<T>? args = null)
            where T : class, IEntity, IHaveSlug
        {
            return GetPrivateQueryableBySlug(slug, args);
        }


        public IQueryable<T> GetQueryable<T>() where T : class, IEntity
        {
            return GetPrivateQueryable(default(SelectCriteria<T>));
        }

        public IQueryable<T> GetQueryable<T>(SelectCriteria<T> criteria) where T : class, IEntity
        {
            return GetPrivateQueryable(criteria);
        }

        #region Create

        public T Create<T>(T entity) where T : class, IEntity
        {
            context.Set<T>().Add(entity);
            return entity;
        }

        public async Task<T> CreateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            await context.Set<T>().AddAsync(entity, cancellationToken);
            return entity;
        }

        public IEnumerable<T> CreateRange<T>(IEnumerable<T> entitys) where T : class, IEntity
        {
            context.Set<T>().AddRange(entitys);
            return entitys;
        }

        public async Task<IEnumerable<T>> CreateRangeAsync<T>(IEnumerable<T> entitys, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            await context.Set<T>().AddRangeAsync(entitys, cancellationToken);
            return entitys;
        }

        public async Task MassiveInsertAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            if (entities.FirstOrDefault() is IAuditableEntity)
            {
                foreach (var entry in entities.Cast<IAuditableEntity>())
                {
                    entry.FillAuditablePropertysAdded(context);
                }
            }

            await context.BulkInsertAsync(entities, bulkConfig, cancellationToken: cancellationToken);
        }

        public async Task MassiveUpsertAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            if (entities.FirstOrDefault() is IAuditableEntity)
            {
                foreach (var entry in entities.Cast<IAuditableEntity>())
                {
                    entry.FillAuditablePropertysAdded(context);
                    entry.FillAuditablePropertysModified(context);
                }

                bulkConfig ??= new BulkConfig();

                if (bulkConfig.PropertiesToInclude is not null)
                {
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.CreatedBy));
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.CreatedOnUtc));
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.LastModifiedBy));
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.LastModifiedOnUtc));
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.LastUpdatedBy));
                    bulkConfig.PropertiesToInclude.Add(nameof(IAuditableEntity.LastUpdatedOnUtc));
                }

                if (bulkConfig.PropertiesToIncludeOnUpdate is not null)
                {
                    bulkConfig.PropertiesToIncludeOnUpdate.Add(nameof(IAuditableEntity.LastModifiedBy));
                    bulkConfig.PropertiesToIncludeOnUpdate.Add(nameof(IAuditableEntity.LastModifiedOnUtc));
                    bulkConfig.PropertiesToIncludeOnUpdate.Add(nameof(IAuditableEntity.LastUpdatedBy));
                    bulkConfig.PropertiesToIncludeOnUpdate.Add(nameof(IAuditableEntity.LastUpdatedOnUtc));
                }
                else
                {
                    bulkConfig.PropertiesToExcludeOnUpdate ??= new List<string>();
                    bulkConfig.PropertiesToExcludeOnUpdate.Add(nameof(IAuditableEntity.CreatedBy));
                    bulkConfig.PropertiesToExcludeOnUpdate.Add(nameof(IAuditableEntity.CreatedOnUtc));
                }

            }

            await context.BulkInsertOrUpdateAsync(entities, bulkConfig, cancellationToken: cancellationToken);
        }

        public async Task MassiveMergeAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            if (entities.FirstOrDefault() is IAuditableEntity)
            {
                foreach (var entry in entities.Cast<IAuditableEntity>())
                {
                    entry.FillAuditablePropertysAdded(context);
                    entry.FillAuditablePropertysModified(context);
                }

                bulkConfig ??= new BulkConfig();

                bulkConfig.PropertiesToExcludeOnUpdate ??= new List<string>();
                bulkConfig.PropertiesToExcludeOnUpdate.Add(nameof(IAuditableEntity.CreatedBy));
                bulkConfig.PropertiesToExcludeOnUpdate.Add(nameof(IAuditableEntity.CreatedOnUtc));
            }

            await context.BulkInsertOrUpdateOrDeleteAsync(entities, bulkConfig, cancellationToken: cancellationToken);
        }

        #endregion

        #region Read

        #region Exists

        public async Task<bool> ExistsAsync<T, TPK>(TPK id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            var query = context.Set<T>().AsNoTracking();
            return await query.AnyAsync(x => x.Id.Equals(id), cancellationToken: cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(int id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<int>
        {
            return await ExistsAsync<T, int>(id, cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(long id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<long>
        {
            return await ExistsAsync<T, long>(id, cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(string id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<string>
        {
            return await ExistsAsync<T, string>(id, cancellationToken);
        }

        public async Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<Guid>
        {
            return await ExistsAsync<T, Guid>(id, cancellationToken);
        }

        #endregion

        #region GetById

        public async Task<T?> GetByIdAsync<T, TPK>(SelectArgs<T, TPK> args, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            return PostProcessing(await GetPrivateQueryable(args).FirstOrDefaultAsync(cancellationToken: cancellationToken), args.Id!, args);
        }

        public async Task<T?> GetByIdAsync<T, TPK>(TPK id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            var argsWithId = new SelectArgs<T, TPK>(args)
            {
                Id = id
            };

            return await GetByIdAsync(argsWithId, cancellationToken);
        }

        public async Task<T?> GetByIdAsync<T>(int id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default) where T : class, IHasIdentity<int>
        {
            return await GetByIdAsync<T, int>(id, args, cancellationToken);
        }

        public async Task<T?> GetByIdAsync<T>(long id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default) where T : class, IHasIdentity<long>
        {
            return await GetByIdAsync<T, long>(id, args, cancellationToken);
        }

        public async Task<T?> GetByIdAsync<T>(string id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default) where T : class, IHasIdentity<string>
        {
            return await GetByIdAsync<T, string>(id, args, cancellationToken);
        }

        public async Task<T?> GetByIdAsync<T>(Guid id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default) where T : class, IHasIdentity<Guid>
        {
            return await GetByIdAsync<T, Guid>(id, args, cancellationToken);
        }

        #endregion

        #region GetBySlug

        public async Task<T?> GetBySlugAsync<T>(string slug, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IEntity, IHaveSlug
        {
            return PostProcessing(await GetPrivateQueryableBySlug(slug, args).FirstOrDefaultAsync(cancellationToken: cancellationToken), slug, args);
        }

        #endregion

        #region GetByName

        public async Task<T?> GetByNameAsync<T>(string name, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IEntity, IHaveName
        {
            return PostProcessing(await GetPrivateQueryableByName(name, args).FirstOrDefaultAsync(cancellationToken: cancellationToken), name, args);
        }

        #endregion

        public async Task<T?> GetSingleAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return PostProcessing(await GetPrivateQueryable(criteria).SingleOrDefaultAsync(cancellationToken), criteria);
        }

        public async Task<T?> GetFirstAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return PostProcessing(await GetPrivateQueryable(criteria).FirstOrDefaultAsync(cancellationToken), criteria);
        }

        public async Task<T?> GetLastAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return PostProcessing(await GetPrivateQueryable(criteria).LastOrDefaultAsync(cancellationToken), criteria);
        }

        public async Task<List<T>> GetAllAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return await GetPrivateQueryable(criteria).ToListAsync(cancellationToken);
        }

        public async Task<List<TResult>> GetAllAsync<T, TResult>(Expression<Func<T, TResult>> select, SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default)
            where T : class, IEntity
        {
            return await GetPrivateQueryable(criteria).Select(select).ToListAsync(cancellationToken);
        }

        public async Task<PaginationResponse<T>> GetSearchResultsAsync<T>(SelectCriteria<T> criteria, CancellationToken cancellationToken = default)
            where T : class, IEntity
        {
            var query = GetPrivateQueryable(criteria)
                .QuerySearch(criteria, out var pagination);

            var list = await query.ToListAsync(cancellationToken);
            var response = new PaginationResponse<T>()
            {
                Pagination = pagination,
                Items = list
            };

            return response;
        }

        public async Task<PaginationResponse<T>> GetSearchResultsAsync<T>(SearchCriteria<T> criteria, CancellationToken cancellationToken = default)
            where T : class, IHaveId<long>, IHaveLastUpdated, IEntity
        {
            var query = GetPrivateQueryable(criteria.Args)
                .QuerySearch(criteria, out var pagination);

            var list = await query.ToListAsync(cancellationToken);
            var response = new PaginationResponse<T>()
            {
                Pagination = pagination,
                Items = list
            };

            return response;
        }

        public async Task<bool> AnyAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return await GetNotTrackedQueryable(criteria).AnyAsync(cancellationToken);
        }

        public async Task<int> CountAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return await GetNotTrackedQueryable(criteria).CountAsync(cancellationToken);
        }

        public async Task<long> LongCountAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return await GetNotTrackedQueryable(criteria).LongCountAsync(cancellationToken);
        }

        private IQueryable<T> GetPrivateQueryable<T>(SelectArgs<T>? args = null) where T : class
            => context.Set<T>().QueryArgs(args);

        private IQueryable<T> GetPrivateQueryableById<T, TPK>(TPK? id, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
            => id is null
            ? GetPrivateQueryable(args).Take(1)
            : GetPrivateQueryable(args).Where(x => x.Id.Equals(id)).Take(1);

        private IQueryable<T> GetPrivateQueryable<T, TPK>(SelectArgs<T, TPK>? args = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
            => GetPrivateQueryableById(args is null ? default : args.Id, args);

        private IQueryable<T> GetPrivateQueryableByName<T>(string name, SelectArgs<T>? args = null) where T : class, IHaveName
            => GetPrivateQueryable(args).Where(x => x.Name.Equals(name)).Take(1);

        private IQueryable<T> GetPrivateQueryableBySlug<T>(string slug, SelectArgs<T>? args = null) where T : class, IHaveSlug
            => GetPrivateQueryable(args).Where(x => x.Slug.Equals(slug)).Take(1);

        private IQueryable<T> GetPrivateQueryable<T>(SelectCriteria<T>? criteria = null) where T : class, IEntity
            => GetPrivateQueryable(criteria?.Args).QuerySelect(criteria);

        private IQueryable<T> GetTrackedQueryable<T>(SelectCriteria<T>? criteria = null) where T : class, IEntity
        {
            criteria ??= new SelectCriteria<T>();
            criteria.Args.Track = true;

            return GetPrivateQueryable(criteria);
        }

        private IQueryable<T> GetNotTrackedQueryable<T>(SelectCriteria<T>? criteria = null) where T : class, IEntity
        {
            criteria ??= new SelectCriteria<T>();
            criteria.Args.Track = false;

            return GetPrivateQueryable(criteria);
        }


        #endregion

        #region Update

        public void Update<T>(T entity) where T : class, IEntity
        {
            context.Entry(entity).State = EntityState.Modified;
        }


#if NET7_0_OR_GREATER

        public async Task<int> ExecuteUpdateAsync<T>(Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            return await ExecuteUpdateAsync(new SelectCriteria<T>(), setPropertyCalls, cancellationToken);
        }

        public async Task<int> ExecuteUpdateAsync<T>(SelectCriteria<T> criteria, Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            var entities = GetTrackedQueryable(criteria);
            return await entities.ExecuteUpdateAsync(setPropertyCalls, cancellationToken);
        }

#endif

        public async Task MassiveUpdateAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            if (entities.FirstOrDefault() is IAuditableEntity)
            {
                foreach (var entry in entities.Cast<IAuditableEntity>())
                {
                    entry.FillAuditablePropertysModified(context);
                }
            }

            await context.BulkUpdateAsync(entities, bulkConfig, cancellationToken: cancellationToken);
        }


        #endregion

        #region Delete

        public void Delete<T>(T entity) where T : class, IEntity
        {
            context.Set<T>().Remove(entity);
        }

        public void DeleteRange<T>(SelectCriteria<T> criteria) where T : class, IEntity
        {
            var entities = GetTrackedQueryable(criteria);
            DeleteRange(entities);
        }

        public void DeleteRange<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            context.Set<T>().RemoveRange(entities);
        }

        public async Task BatchDeleteAsync<T>(SelectCriteria<T> criteria, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            var entities = GetTrackedQueryable(criteria);
            await entities.BatchDeleteAsync(cancellationToken);
        }

        public async Task MassiveDeleteAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity
        {
            if (entities.FirstOrDefault() is ISoftDelete)
            {
                foreach (var entry in entities.Cast<ISoftDelete>())
                {
                    entry.DeletedBy = null;
                    entry.DeletedOnUtc = DateTime.Now;
                }

                await context.BulkUpdateAsync(entities, bulkConfig, cancellationToken: cancellationToken);
                return;
            }

            await context.BulkDeleteAsync(entities, bulkConfig, cancellationToken: cancellationToken);
        }

        #region DeleteById

        public async Task DeleteByIdAsync<T, TPK>(TPK id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull
        {
            var entity = await GetByIdAsync<T, TPK>(id, cancellationToken: cancellationToken);
            if (entity != null)
            {
                Delete(entity);
            }
        }

        public Task DeleteByIdAsync<T>(int id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<int>
        {
            return DeleteByIdAsync<T, int>(id, cancellationToken);
        }

        public Task DeleteByIdAsync<T>(long id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<long>
        {
            return DeleteByIdAsync<T, long>(id, cancellationToken);
        }

        public Task DeleteByIdAsync<T>(string id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<string>
        {
            return DeleteByIdAsync<T, string>(id, cancellationToken);
        }

        public Task DeleteByIdAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<Guid>
        {
            return DeleteByIdAsync<T, Guid>(id, cancellationToken);
        }

        #endregion

        #endregion

        #region UnitOfWork

        private IDbContextTransaction? Transaction
        {
            get
            {
                try
                {
                    return context.Database.CurrentTransaction;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public bool HasTransaction => Transaction != null;

        public RepositoryTransaction CreateTransaction()
        {
            var transaction = context.Database.BeginTransaction();
            return new RepositoryTransaction(transaction);
        }

        public async Task<RepositoryTransaction> CreateTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            return new RepositoryTransaction(transaction);
        }

        public int Save()
        {
            return context.SaveChanges();
        }

        public async Task<int> SaveAsync(CancellationToken cancellationToken)
        {
            return await context.SaveChangesAsync(cancellationToken);
        }

        public void Commit()
        {
            ArgumentNullException.ThrowIfNull(Transaction);

            Transaction.Commit();
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(Transaction);

            await Transaction.CommitAsync(cancellationToken);
        }

        public void Rollback()
        {
            ArgumentNullException.ThrowIfNull(Transaction);

            Transaction.Rollback();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(Transaction);

            await Transaction.RollbackAsync(cancellationToken);
        }

        public void Reload(IEntity entity)
        {
            context.Entry(entity).Reload();
        }

        public async Task ReloadAsync(IEntity entity, CancellationToken cancellationToken)
        {
            await context.Entry(entity).ReloadAsync(cancellationToken);
        }

        public void Attach<T>(T entity)
            where T : class, IEntity
        {
            context.Set<T>().Attach(entity);
        }

        public void AttachRange<T>(params T[] entity)
            where T : class, IEntity
        {
            context.Set<T>().AttachRange(entity);
        }

        public void Detach(IEntity entity)
        {
            context.Entry(entity).State = EntityState.Detached;
        }

        void DetachGraph(IEntity entity)
        {
            var entry = context.Entry(entity);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }

            foreach (var navigation in entry.Navigations)
            {
                if (navigation.CurrentValue is IEnumerable<IEntity> collection)
                {
                    foreach (var child in collection)
                    {
                        DetachGraph(child);

                    }
                }
                else if (navigation.CurrentValue is IEntity navigationEntity)
                {
                    DetachGraph(navigationEntity);
                }
            }
        }

        public void DetachAll()
        {
            context.ChangeTracker.Clear();
        }

        #endregion

        protected T CreateInstanceRepository<T>()
        {
            var key = typeof(T).Name;
            if (!repositorys.ContainsKey(key))
            {
                var instance = CreateRepositoryInstance<T>();
                repositorys.Add(key, instance);
            }

            return (T)repositorys[key]!;
        }

        protected virtual object CreateRepositoryInstance<T>() => Activator.CreateInstance(typeof(T), this, logger) ?? throw new Exception($"Cannot instanciate {typeof(T).FullName}");

        public DbCommand LoadStoredProcedure(string name, bool prependDefaultSchema = true, short commandTimeout = 30)
        {
            return context.LoadStoredProc(name, prependDefaultSchema, commandTimeout);
        }

        public void Dispose() => context.Dispose();


        private static T PostProcessing<T>(T result, SelectCriteria<T>? criteria)
        {
            criteria ??= new();

            return PostProcessing(result, criteria, criteria.Args);
        }

        private static T PostProcessing<T>(T result, object key, BaseSelectArgs? args)
        {
            if (args == null) return result;

            if (args.ThrowExceptionIfNull && result == null)
            {
                throw new NotFoundException(typeof(T), key);
            }

            return result;
        }
    }
}
