using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fbognini.Core.Domain.Query;

public class PageCriteria
{
    public int? Number { get; internal set; }
    public int? Size { get; internal set; }

    /// <summary>
    /// Take(MaxTake) used during list.Count().
    /// Usefull for performance issues -> I won't show 182_000 results, but +10_000 results.
    /// </summary>
    public int? MaxTake { get; internal set; }

    [Obsolete("Use QueryPagination with no out PaginationResult parameter")]
    internal int? Total { get; set; }
    [Obsolete("Use QueryPagination with no out PaginationResult parameter")]
    internal bool? AtLeast { get; set; }
}
