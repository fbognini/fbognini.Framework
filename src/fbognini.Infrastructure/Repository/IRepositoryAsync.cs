using EFCore.BulkExtensions;
using fbognini.Core.Domain;
using fbognini.Core.Domain.Query;
using fbognini.Core.Domain.Query.Pagination;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Repository
{
    public interface IRepositoryAsync
    {
        #region Database

        string? GetConnectionString();

#if NET7_0_OR_GREATER
        Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken cancellationToken = default);
#endif

        #endregion

        IQueryable<T> GetQueryable<T, TPK>(SelectArgs<T, TPK>? criteria = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull;

        IQueryable<T> GetQueryableById<T, TPK>(TPK key, SelectArgs<T>? args = null)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull;
        IQueryable<T> GetQueryableById<T>(int id, SelectArgs<T>? args = null) where T : class, IHasIdentity<int>;
        IQueryable<T> GetQueryableById<T>(long id, SelectArgs<T>? args = null) where T : class, IHasIdentity<long>;
        IQueryable<T> GetQueryableById<T>(string id, SelectArgs<T>? args = null) where T : class, IHasIdentity<string>;
        IQueryable<T> GetQueryableById<T>(Guid id, SelectArgs<T>? args = null) where T : class, IHasIdentity<Guid>;

        IQueryable<T> GetQueryableByName<T>(string name, SelectArgs<T>? args = null) where T : class, IEntity, IHaveName;
        IQueryable<T> GetQueryableBySlug<T>(string slug, SelectArgs<T>? args = null) where T : class, IEntity, IHaveSlug;

        IQueryable<T> GetQueryable<T>() where T : class, IEntity;
        IQueryable<T> GetQueryable<T>(SelectCriteria<T> criteria) where T : class, IEntity;

        #region Create
        T Create<T>(T entity) where T : class, IEntity;
        Task<T> CreateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IEntity;

        IEnumerable<T> CreateRange<T>(IEnumerable<T> entitys) where T : class, IEntity;
        Task<IEnumerable<T>> CreateRangeAsync<T>(IEnumerable<T> entitys, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task MassiveInsertAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity;

        Task MassiveUpsertAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task MassiveMergeAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity;

        #endregion

        #region Read

        #region Exists

        Task<bool> ExistsAsync<T, TPK>(TPK id, CancellationToken cancellationToken = default)
        where T : class, IHasIdentity<TPK>
        where TPK : notnull;

        Task<bool> ExistsAsync<T>(int id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<int>;

        Task<bool> ExistsAsync<T>(long id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<long>;

        Task<bool> ExistsAsync<T>(string id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<string>;

        Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<Guid>;

        #endregion

        #region GetById

        Task<T?> GetByIdAsync<T, TPK>(SelectArgs<T, TPK> args, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull;

        Task<T?> GetByIdAsync<T, TPK>(TPK id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull;

        Task<T?> GetByIdAsync<T>(int id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<int>;

        Task<T?> GetByIdAsync<T>(long id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<long>;

        Task<T?> GetByIdAsync<T>(string id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<string>;

        Task<T?> GetByIdAsync<T>(Guid id, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<Guid>;

        #endregion

        #region GetByName

        Task<T?> GetByNameAsync<T>(string slug, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IEntity, IHaveName;

        #endregion

        #region GetBySlug

        Task<T?> GetBySlugAsync<T>(string slug, SelectArgs<T>? args = null, CancellationToken cancellationToken = default)
            where T : class, IEntity, IHaveSlug;

        #endregion

        Task<T?> GetSingleAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<T?> GetFirstAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<T?> GetLastAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<List<T>> GetAllAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;

        Task<PaginationResponse<T>> GetSearchResultsAsync<T>(SelectCriteria<T> criteria, CancellationToken cancellationToken = default)
            where T : class, IEntity;

        Task<PaginationResponse<T>> GetSearchResultsAsync<T>(SearchCriteria<T> criteria, CancellationToken cancellationToken = default)
            where T : class, IHaveId<long>, IHaveLastUpdated, IEntity;

        Task<bool> AnyAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<int> CountAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<long> LongCountAsync<T>(SelectCriteria<T>? criteria = null, CancellationToken cancellationToken = default) where T : class, IEntity;

        #endregion

        #region Update

        void Update<T>(T entity) where T : class, IEntity;


#if NET7_0_OR_GREATER

        Task<int> ExecuteUpdateAsync<T>(Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls, CancellationToken cancellationToken = default) where T : class, IEntity;
        Task<int> ExecuteUpdateAsync<T>(SelectCriteria<T> criteria, Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls, CancellationToken cancellationToken = default) where T : class, IEntity;

#endif

        Task MassiveUpdateAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity;


        #endregion

        #region Delete

        void Delete<T>(T entity) where T : class, IEntity;
        void DeleteRange<T>(SelectCriteria<T> criteria) where T : class, IEntity;
        void DeleteRange<T>(IEnumerable<T> entitys) where T : class, IEntity;

        #region DeleteById

        Task DeleteByIdAsync<T, TPK>(TPK id, CancellationToken cancellationToken = default)
            where T : class, IHasIdentity<TPK>
            where TPK : notnull;

        Task DeleteByIdAsync<T>(int id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<int>;

        Task DeleteByIdAsync<T>(long id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<long>;

        Task DeleteByIdAsync<T>(string id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<string>;

        Task DeleteByIdAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IHasIdentity<Guid>;

        #endregion

        Task BatchDeleteAsync<T>(SelectCriteria<T> criteria, CancellationToken cancellationToken = default) where T : class, IEntity;

        Task MassiveDeleteAsync<T>(IList<T> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default) where T : class, IEntity;

        #endregion

        #region UnitOfWork

        bool HasTransaction { get; }

        RepositoryTransaction CreateTransaction();
        Task<RepositoryTransaction> CreateTransactionAsync(CancellationToken cancellationToken);

        int Save();
        Task<int> SaveAsync(CancellationToken cancellationToken);

        [Obsolete("Use RepositoryTransaction.Commit instead")]
        void Commit();
        [Obsolete("Use RepositoryTransaction.CommitAsync instead")]
        Task CommitAsync(CancellationToken cancellationToken);

        [Obsolete("Use RepositoryTransaction.Rollback instead")]
        void Rollback();
        [Obsolete("Use RepositoryTransaction.RollbackAsync instead")]
        Task RollbackAsync(CancellationToken cancellationToken);

        void Reload(IEntity entity);
        Task ReloadAsync(IEntity entity, CancellationToken cancellationToken);
        void Attach<T>(T entity)
            where T : class, IEntity;
        void AttachRange<T>(params T[] entity)
            where T : class, IEntity;
        void Detach(IEntity entity);
        void DetachAll();

        #endregion

        DbCommand LoadStoredProcedure(string name, bool prependDefaultSchema = true, short commandTimeout = 30);
    }
}
