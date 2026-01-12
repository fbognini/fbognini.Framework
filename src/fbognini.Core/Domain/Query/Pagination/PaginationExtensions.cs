using fbognini.Core.Domain.Query;
using System;
using System.Linq;

namespace fbognini.Core.Domain.Query.Pagination
{
    public static class PaginationExtensions
    {
        public static IQueryable<T> QueryPagination<T>(this IQueryable<T> list, QueryableCriteria<T> offsetCriteria)
        {
            return list.QueryPagination(offsetCriteria, out var _);
        }

        public static IQueryable<T> QueryPagination<T, TKey>(this IQueryable<T> list, QueryableAuditableCriteria<T> searchCriteria)
            where T : IHaveId<long>, IHaveLastUpdated
        {
            return list.QueryPagination(searchCriteria, out var _);
        }

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

        private static bool InitializePagination<T>(IQueryable<T> list, PageCriteria page, out PaginationResult? pagination)
        {
            if (!page.Size.HasValue)
            {
                pagination = null;
                return false;
            }

            pagination = new PaginationResult();

            UpdatePageCriteriaTotals(page, list);

            pagination.Total = page.Total;
            pagination.AtLeast = page.AtLeast;

            if (page.Size != -1)
            {
                return true;
            }

            return false;
        }

        private static void UpdatePageCriteriaTotals<T>(PageCriteria page, IQueryable<T> list)
        {
            if (!page.MaxTake.HasValue)
            {
                page.Total = list.Count();
                page.AtLeast = false;
                return;
            }

            var take = page.MaxTake.Value + 1;
            var total = list.Take(take).Count();
            if (total != take)
            {
                page.Total = total;
                page.AtLeast = false;
                return;
            }

            page.Total = total - 1;
            page.AtLeast = true;
        }
    }
}
