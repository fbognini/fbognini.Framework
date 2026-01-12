using fbognini.Core.Domain.Query.Pagination;
using fbognini.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace fbognini.Core.Domain.Query
{
    public class QueryableCriteria<T> : IHasFilter<T>, IHasSearch<T>, IHasSorting<T>, IArgs
    {
        private readonly List<KeyValuePair<string, SortingDirection>> _sorting = new();

        public QueryableCriteria()
        {
        }

        public virtual PageCriteria Page { get; } = new();
        public IReadOnlyList<KeyValuePair<string, SortingDirection>> Sorting => _sorting;
        public Search<T> Search { get; } = new Search<T>();

        protected LogicalOperator Operator { get; set; } = LogicalOperator.AND;

        public virtual List<Expression<Func<T, bool>>> ToWhereClause() => new List<Expression<Func<T, bool>>>();

        public Expression<Func<T, bool>> ResolveFilter()
        {
            var list = ToWhereClause();
            return list.BuildPredicate(Operator);
        }


        public QueryableCriteria<T> AddSorting(string criteria, SortingDirection direction)
        {
            _sorting.Add(new KeyValuePair<string, SortingDirection>(criteria, direction));
            return this;
        }

        public QueryableCriteria<T> AddSorting(Expression<Func<T, object?>> criteria, SortingDirection direction)
        {
            return AddSorting(criteria.GetPropertyPath(true), direction);
        }

        public void ClearSorting()
        {
            _sorting.Clear();
        }

        public QueryableCriteria<T> LoadPaginationOffsetQuery(PaginationOffsetQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            Page.Number = query.PageNumber;
            Page.Size = query.PageSize;

            return this;
        }

        public QueryableCriteria<T> LimitPaginationCount(int limit)
        {
            if (limit < 0)
            {
                throw new ArgumentException("Non-negative number is required", nameof(limit));
            }

            Page.MaxTake = limit;

            return this;
        }

        public void SetOperator(LogicalOperator logicalOperator)
        {
            Operator = logicalOperator;
        }

        public virtual string GetArgsKey()
        {
            var builder = GetQueryableArgsBuilder();

            return builder.ToString();
        }

        public virtual Dictionary<string, object?> GetArgsKeyAsDictionary()
        {
            var dictionary = GetQueryableArgsAsDisctionaryBuilder();

            return dictionary.ToDictionary(x => x.Key, x => x.Value);
        }

        protected StringBuilder GetQueryableArgsBuilder()
        {
            var builder = new StringBuilder();
            builder.Append(typeof(T).Name);

            builder.Append((this as IHasFilter<T>).GetArgsKey());
            builder.Append((this as IHasSearch<T>).GetArgsKey());
            builder.Append((this as IHasSorting<T>).GetArgsKey());

            return builder;
        }

        protected List<KeyValuePair<string, object?>> GetQueryableArgsAsDisctionaryBuilder()
        {
            var dictionary = new List<KeyValuePair<string, object?>>
            {
                new("_Entity", typeof(T).Name)
            };

            dictionary.AddRange((this as IHasFilter<T>).GetArgsKeyAsDictionary());
            dictionary.AddRange((this as IHasSearch<T>).GetArgsKeyAsDictionary());
            dictionary.AddRange((this as IHasSorting<T>).GetArgsKeyAsDictionary());

            return dictionary;
        }
    }

}
