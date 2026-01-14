using fbognini.Core.Domain.Query;
using System;
using System.Linq;

namespace fbognini.Core.Domain.Query.Pagination
{
    public static class PaginationExtensions
    {
        public static IQueryable<T> QueryPagination<T>(this IQueryable<T> list, QueryableCriteria<T> criteria)
        {
            var page = criteria.Page;

            if (!page.Size.HasValue || page.Size == -1)
            {
                return list;
            }

            if (!page.Number.HasValue)
            {
                return list;
            }

            return list
                .Skip((page.Number.Value - 1) * page.Size.Value)
                .Take(page.Size.Value);
        }

        public static IQueryable<T> QueryPagination<T, TKey>(this IQueryable<T> list, QueryableAuditableCriteria<T> criteria)
            where T : IHaveId<long>, IHaveLastUpdated
        {
            var page = criteria.Page;

            if (!page.Size.HasValue || page.Size == -1)
            {
                return list;
            }

            if (page.Number.HasValue)
            {
                return list
                    .Skip((page.Number.Value - 1) * page.Size.Value)
                    .Take(page.Size.Value);
            }

            // Cursor-based pagination
            if (!page.Since.HasValue)
            {
                return list;
            }

            var since = DateTime.UnixEpoch.AddMilliseconds(page.Since.Value);

            list = list.Where(x => x.LastUpdatedOnUtc >= since);

            if (page.AfterId.HasValue)
            {
                list = list.Where(x => x.Id > page.AfterId.Value);
            }

            return list
                .OrderBy(x => x.LastUpdatedOnUtc)
                .Take(page.Size.Value);
        }

        public static PaginationResult? CalculatePaginationResult<T>(IQueryable<T> list, QueryableCriteria<T> criteria) => CalculatePaginationResult(list, page: criteria.Page);


        public static PaginationResult? CalculatePaginationResult<T>(IQueryable<T> list, QueryableAuditableCriteria<T> criteria)
            where T : IHaveId<long>, IHaveLastUpdated
        {
            var page = criteria.Page;

            var pagination = CalculatePaginationResult(list, page);
            if (pagination is null)
            {
                return null;
            }

            // Offset-based
            if (page.Number.HasValue)
            {
                return pagination;
            }

            // Since-based
            if (page.Since.HasValue)
            {
                var since = DateTime.UnixEpoch.AddMilliseconds(page.Since.Value);

                var filtered = list.Where(x => x.LastUpdatedOnUtc >= since);

                if (page.AfterId.HasValue)
                {
                    filtered = filtered.Where(x => x.Id > page.AfterId.Value);
                }

                pagination.PartialTotal = filtered.Count();

                var last = filtered.OrderBy(x => x.LastUpdatedOnUtc).Take(page.Size!.Value).LastOrDefault();
                if (last != null)
                {
                    long continuationSince = Convert.ToInt64(last.LastUpdatedOnUtc.Subtract(DateTime.MinValue.AddYears(1969)).TotalMilliseconds);
                    string continuation = $"{continuationSince}_{last.Id}";

                    pagination.ContinuationSince = continuation;
                }
                else
                {
                    pagination.ContinuationSince = page.Since.ToString();
                    if (page.AfterId != null)
                    {
                        pagination.ContinuationSince += "_" + page.AfterId;
                    }
                }

                return pagination;
            }


            throw new InvalidOperationException("Invalid pagination criteria.");
        }

        private static PaginationResult? CalculatePaginationResult<T>(IQueryable<T> list, PageCriteria page)
        {
            if (!page.Size.HasValue)
            {
                return null;
            }

            var result = new PaginationResult
            {
                PageSize = page.Size.Value,
                PageNumber = page.Number
            };

            if (!page.MaxTake.HasValue)
            {
                result.Total = list.Count();
                result.AtLeast = false;
                return result;
            }

            var take = page.MaxTake.Value + 1;
            var total = list.Take(take).Count();
            if (total != take)
            {
                result.Total = total;
                result.AtLeast = false;
                return result;
            }

            result.Total = total - 1;
            result.AtLeast = true;
            return result;
        }


        [Obsolete("Use QueryPagination with out PaginationResult parameter")]
        public static IQueryable<T> QueryPagination<T>(this IQueryable<T> list, QueryableCriteria<T> criteria, out PaginationResult? pagination)
        {
            var page = criteria.Page;

            if (!InitializePagination(list, page, out pagination))
            {
                return list;
            }
            pagination!.PageSize = page.Size!.Value;

            if (!page.Number.HasValue)
            {
                return list;
            }

            pagination.PageNumber = page.Number.Value;

            return list
                .Skip((page.Number.Value - 1) * page.Size.Value)
                .Take(page.Size.Value);
        }


        [Obsolete("Use QueryPagination with no out PaginationResult parameter")]
        public static IQueryable<T> QueryPagination<T>(this IQueryable<T> list, QueryableAuditableCriteria<T> criteria, out PaginationResult? pagination)
            where T : IHaveId<long>, IHaveLastUpdated
        {
            var page = criteria.Page;

            if (!InitializePagination(list, page, out pagination))
            {
                return list;
            }
            pagination!.PageSize = page.Size!.Value;

            if (page.Number.HasValue)
            {
                pagination.PageNumber = page.Number.Value;

                return list
                    .Skip((page.Number.Value - 1) * page.Size.Value)
                    .Take(page.Size.Value);
            }

            if (page.Since.HasValue)
            {
                var since = new DateTime(1970, 1, 1).AddTicks(page.Since.Value * 10000);
                list = list.Where(x => x.LastUpdatedOnUtc >= since);

                if (page.AfterId is not null)
                {
                    list = list.Where(x => x.Id > page.AfterId.Value);
                }

                pagination.PartialTotal = list.Count();

                list = list
                    .OrderBy(x => x.LastUpdatedOnUtc)
                    .Take(page.Size.Value);

                var last = list.LastOrDefault();
                if (last != null)
                {
                    long continuationSince = Convert.ToInt64(last.LastUpdatedOnUtc.Subtract(DateTime.MinValue.AddYears(1969)).TotalMilliseconds);
                    string continuation = $"{continuationSince}_{last.Id}";

                    pagination.ContinuationSince = continuation;
                    page.ContinuationSince = continuation;
                }
                else
                {
                    pagination.ContinuationSince = page.Since.ToString();
                    if (page.AfterId != null)
                    {
                        pagination.ContinuationSince += "_" + page.AfterId;
                    }
                }
            }

            return list;
        }


        [Obsolete("Use QueryPagination with no out PaginationResult parameter")]
        private static bool InitializePagination<T>(IQueryable<T> list, PageCriteria page, out PaginationResult? pagination)
        {
            if (!page.Size.HasValue)
            {
                pagination = null;
                return false;
            }

            pagination = new PaginationResult();


            if (!page.MaxTake.HasValue)
            {
                page.Total = list.Count();
                page.AtLeast = false;
            }
            else
            {

                var take = page.MaxTake.Value + 1;
                var total = list.Take(take).Count();
                if (total != take)
                {
                    page.Total = total;
                    page.AtLeast = false;
                }
                else
                {
                    page.Total = total - 1;
                    page.AtLeast = true;
                }
            }

            pagination.Total = page.Total;
            pagination.AtLeast = page.AtLeast;

            return page.Size != -1;
        }

    }
}
