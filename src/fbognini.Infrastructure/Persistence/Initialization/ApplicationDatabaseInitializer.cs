using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Persistence.Initialization
{

    public class ApplicationDatabaseInitializer<TContext>
        where TContext : DbContext
    {
        private readonly TContext dbContext;
        private readonly IMultiTenantContextAccessor multiTenantContextAccessor;
        private readonly ApplicationSeederRunner<TContext> dbSeeder;
        private readonly ILogger<ApplicationDatabaseInitializer<TContext>> logger;

        public ApplicationDatabaseInitializer(TContext dbContext, IMultiTenantContextAccessor multiTenantContextAccessor, ApplicationSeederRunner<TContext> dbSeeder, ILogger<ApplicationDatabaseInitializer<TContext>> logger)
        {
            this.dbContext = dbContext;
            this.multiTenantContextAccessor = multiTenantContextAccessor;
            this.dbSeeder = dbSeeder;
            this.logger = logger;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var currentTenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo;

            if (dbContext.Database.GetMigrations().Any())
            {
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Applying migrations ({@Migrations}) for '{TenantIdentifier}' tenant.", pendingMigrations, currentTenant?.Identifier);
                    await dbContext.Database.MigrateAsync(cancellationToken);
                }
            }

            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                logger.LogInformation("Connection to {TenantIdentifier}'s Database Succeeded.", currentTenant?.Identifier);

                await dbSeeder.RunSeedersAsync(dbContext, cancellationToken);
            }
        }
    }


}