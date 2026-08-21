using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Persistence.ValueConverters
{
    public class UtcDateConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateConverter()
        : base(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        { }
    }
}
