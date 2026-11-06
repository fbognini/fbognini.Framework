using System.Linq;

namespace fbognini.Infrastructure.Outbox
{
    internal static class OutboxApplicationFilter
    {
        public static IQueryable<OutboxMessage> Apply(IQueryable<OutboxMessage> query, OutboxSettings settings, string applicationName)
        {
            return settings.ApplicationFilter switch
            {
                OutboxProcessorApplicationFilter.Me => query.Where(x => x.Application == applicationName),
                OutboxProcessorApplicationFilter.Custom => ApplyCustom(query, settings.Applications),
                OutboxProcessorApplicationFilter.All => query,
                _ => query.Where(x => false),
            };
        }

        // Custom without a list would otherwise read as All: an empty list processes nothing instead.
        private static IQueryable<OutboxMessage> ApplyCustom(IQueryable<OutboxMessage> query, string? applications)
        {
            var names = (applications ?? string.Empty)
                .Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .ToList();

            return query.Where(x => names.Contains(x.Application));
        }
    }
}
