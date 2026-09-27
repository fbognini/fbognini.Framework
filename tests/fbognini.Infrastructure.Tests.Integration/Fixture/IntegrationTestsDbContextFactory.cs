using fbognini.Core.Interfaces;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace fbognini.Infrastructure.Tests.Integration.Fixture
{
    internal class IntegrationTestsDbContextFactory : IDbContextFactory<IntegrationTestsDbContext>
    {
        private readonly DbContextOptions<IntegrationTestsDbContext> options;
        private readonly IOptions<DatabaseSettings> databaseOptions;
        private readonly ICurrentUserService currentUserService;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IOutboxMessagesListener outboxMessagesListener;
        private readonly IMultiTenantContextAccessor multiTenantContextAccessor;

        public IntegrationTestsDbContextFactory(
            DbContextOptions<IntegrationTestsDbContext> options,
            IOptions<DatabaseSettings> databaseOptions,
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider,
            IOutboxMessagesListener outboxMessagesListener,
            IMultiTenantContextAccessor multiTenantContextAccessor)
        {
            this.options = options;
            this.databaseOptions = databaseOptions;
            this.currentUserService = currentUserService;
            this.dateTimeProvider = dateTimeProvider;
            this.outboxMessagesListener = outboxMessagesListener;
            this.multiTenantContextAccessor = multiTenantContextAccessor;
        }

        public IntegrationTestsDbContext CreateDbContext()
        {
            return new IntegrationTestsDbContext(options, databaseOptions, currentUserService, dateTimeProvider, outboxMessagesListener, multiTenantContextAccessor);
        }
    }
}
