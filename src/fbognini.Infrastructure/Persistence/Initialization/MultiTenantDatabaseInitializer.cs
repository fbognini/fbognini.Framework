using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Persistence.Initialization
{
    internal class EFCoreMultiTenantDatabaseInitializer<TContext, TTenantInfo> : IMultiTenantDatabaseInitializer<TTenantInfo>
        where TContext : DbContext
        where TTenantInfo : Tenant, new()
    {
        protected readonly IServiceProvider serviceProvider;
        protected readonly IMultiTenantStore<TTenantInfo> store;
        protected readonly ILogger<EFCoreMultiTenantDatabaseInitializer<TContext, TTenantInfo>> logger;

        public EFCoreMultiTenantDatabaseInitializer(
            IServiceProvider serviceProvider,
            IMultiTenantStore<TTenantInfo> store,
            ILogger<EFCoreMultiTenantDatabaseInitializer<TContext, TTenantInfo>> logger)
        {
            this.serviceProvider = serviceProvider;
            this.store = store;
            this.logger = logger;
        }

        public virtual async Task InitializeDatabasesAsync(CancellationToken cancellationToken)
        {
            var tenants = await store.GetAllAsync();
            foreach (var tenant in tenants.Where(x => x.IsActive))
            {
                await InitializeApplicationDbForTenantAsync(tenant, cancellationToken);
            }

            logger.LogInformation("Multitenancy (ef core) initialization completed");
        }

        public async Task InitializeApplicationDbForTenantAsync(TTenantInfo tenant, CancellationToken cancellationToken)
        {
            // First create a new scope
            using var scope = serviceProvider.CreateScope();

            // Then set current tenant so the right connectionstring is used
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
                .SetCurrentTenant(tenant);

            await scope.ServiceProvider
                .GetRequiredService<ApplicationDatabaseInitializer<TContext>>()
                .InitializeAsync(cancellationToken);
        }
    }


    internal class EFCoreMultiTenantDatabaseInitializer<TContext, TTenantContext, TTenantInfo> : EFCoreMultiTenantDatabaseInitializer<TContext, TTenantInfo>, IMultiTenantDatabaseInitializer<TTenantInfo>
        where TContext : DbContext
        where TTenantContext: TenantDbContext<TTenantInfo>
        where TTenantInfo : Tenant, new()
    {
        private readonly TTenantContext tenantDbContext;

        public EFCoreMultiTenantDatabaseInitializer(
            IServiceProvider serviceProvider,
            IMultiTenantStore<TTenantInfo> store,
            TTenantContext tenantDbContext,
            ILogger<EFCoreMultiTenantDatabaseInitializer<TContext, TTenantInfo>> logger)
            : base(serviceProvider, store, logger)
        {
            this.tenantDbContext = tenantDbContext;
        }

        public override async Task InitializeDatabasesAsync(CancellationToken cancellationToken)
        {
            await InitializeTenantDbAsync(cancellationToken);

            await base.InitializeDatabasesAsync(cancellationToken);
        }

        private async Task InitializeTenantDbAsync(CancellationToken cancellationToken)
        {
            if (tenantDbContext.Database.GetPendingMigrations().Any())
            {
                logger.LogInformation("Applying Root Migrations.");
                await tenantDbContext.Database.MigrateAsync(cancellationToken);
            }

            await SeedRootTenantAsync(cancellationToken);
        }

        private async Task SeedRootTenantAsync(CancellationToken cancellationToken)
        {
            if (await tenantDbContext.TenantInfo.FirstOrDefaultAsync(x => x.Identifier == MultitenancyConstants.Root.Key, cancellationToken) is null)
            {
                var rootTenant = new TTenantInfo
                {
                    Identifier = MultitenancyConstants.Root.Key,
                    Name = MultitenancyConstants.Root.Name,
                    ConnectionString = string.Empty,
                    AdminEmail = MultitenancyConstants.Root.EmailAddress,
                    IsActive = true,
                    Issuer = null,
                    ValidUpto = DateTime.UtcNow.AddYears(1)
                };

                tenantDbContext.TenantInfo.Add(rootTenant);

                await tenantDbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

}