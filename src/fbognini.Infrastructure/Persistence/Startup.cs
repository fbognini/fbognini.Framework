using fbognini.Infrastructure.Common;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Multitenancy;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence.ConnectionString;
using fbognini.Infrastructure.Persistence.Initialization;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace fbognini.Infrastructure.Persistence
{
    public static class Startup
    {
        private static IServiceCollection AddBasePersistence<T>(this IServiceCollection services, IConfiguration configuration, Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptions = null)
            where T : DbContext, IBaseDbContext
        {
            LinqToDBForEFTools.Initialize();

            // TODO: We should probably add specific dbprovider/connectionstring setting for the tenantDb with a fallback to the main databasesettings
            var databaseSettings = GetDatabaseSettingsAndGuard(configuration);

            return services
                .Configure<DatabaseSettings>(configuration.GetSection(nameof(DatabaseSettings)))
                .AddDbContextFactory<T>((provider, contextOptions) =>
                {
                    contextOptions.ConfigureDbProvider(databaseSettings.DBProvider!, databaseSettings.ConnectionString!);
                    contextOptions.UseQueryTrackingBehavior(databaseSettings.TrackingBehavior);

                    configureDbContextOptions?.Invoke(provider, contextOptions);

                }, lifetime: ServiceLifetime.Scoped) // Needed lifetime scoped to avoid issue "Cannot resolve scoped service ICurrentUserService from root provider."
                .AddTransient<ApplicationDatabaseInitializer<T>>()
                .AddImplementations(typeof(ICustomSeeder<T>), ServiceLifetime.Transient)
                .AddTransient<ApplicationSeederRunner<T>>()
                .AddTransient<IConnectionStringSecurer, ConnectionStringSecurer>()
                .AddTransient<IConnectionStringValidator, ConnectionStringValidator>();
        }

        public static FinbuckleMultiTenantBuilder<TTenant> AddPersistenceAndMultitenancy<T, TTenantContext, TTenant>(this IServiceCollection services, IConfiguration configuration, Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptions = null)
            where T : DbContext, IBaseDbContext
            where TTenantContext : TenantDbContext<TTenant>
            where TTenant : Tenant, new()
        {
            services.AddDbContext<TTenantContext>((provider, m) =>
            {
                var databaseSettings = GetDatabaseSettingsAndGuard(configuration);
                m.ConfigureDbProvider(databaseSettings.DBProvider, databaseSettings.ConnectionString!);

                configureDbContextOptions?.Invoke(provider, m);
            });

            return services
                .AddBasePersistence<T>(configuration, configureDbContextOptions)
                .AddOutboxProcessing<T, TTenant>()
                .AddMultiTenantInitializer<T, TTenantContext, TTenant>()
                .AddMultitenancy<TTenantContext, TTenant>(configuration);
        }


        public static FinbuckleMultiTenantBuilder<TTenant> AddPersistenceAndMultitenancy<T, TTenant>(this IServiceCollection services, IConfiguration configuration, Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptions = null)
            where T : DbContext, IBaseDbContext
            where TTenant : Tenant, new()
        {
            return services.AddBasePersistence<T>(configuration, configureDbContextOptions)
                .AddOutboxProcessing<T, TTenant>()
                .AddMultiTenantInitializer<T, TTenant>()
                .AddMultitenancy<TTenant>(configuration);
        }

        public static FinbuckleMultiTenantBuilder<Tenant> AddPersistenceAndMultitenancy<T>(this IServiceCollection services, IConfiguration configuration, Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptions = null)
            where T : DbContext, IBaseDbContext
        {
            return services.AddPersistenceAndMultitenancy<T, Tenant>(configuration, configureDbContextOptions);
        }

        public static IServiceCollection AddPersistence<T>(this IServiceCollection services, IConfiguration configuration, Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptions = null)
            where T : DbContext, IBaseDbContext
        {
            return services.AddBasePersistence<T>(configuration, configureDbContextOptions);
        }

        public static IServiceCollection AddMultiTenantInitializer<T, TTenantContext, TTenant>(this IServiceCollection services)
            where T : DbContext, IBaseDbContext
            where TTenantContext : TenantDbContext<TTenant>
            where TTenant : Tenant, new()
        {
            return services
                .AddTransient<IMultiTenantDatabaseInitializer, EFCoreMultiTenantDatabaseInitializer<T, TTenantContext, TTenant>>()
                .AddTransient<IMultiTenantDatabaseInitializer<TTenant>, EFCoreMultiTenantDatabaseInitializer<T, TTenantContext, TTenant>>();
        }

        public static IServiceCollection AddMultiTenantInitializer<T, TTenant>(this IServiceCollection services)
            where T : DbContext, IBaseDbContext
            where TTenant : Tenant, new()
        {
            return services
                .AddTransient<IMultiTenantDatabaseInitializer, EFCoreMultiTenantDatabaseInitializer<T, TTenant>>()
                .AddTransient<IMultiTenantDatabaseInitializer<TTenant>, EFCoreMultiTenantDatabaseInitializer<T, TTenant>>();
        }

        public static IServiceCollection AddMultiTenantInitializer<T>(this IServiceCollection services)
            where T : DbContext, IBaseDbContext
        {
            return services.AddMultiTenantInitializer<T, Tenant>();
        }

        private static DatabaseSettings GetDatabaseSettingsAndGuard(IConfiguration configuration)
        {
            var databaseSettings = configuration.GetSection(nameof(DatabaseSettings)).Get<DatabaseSettings>();
            if (databaseSettings is null)
            {
                throw new InvalidOperationException("DatabaseSettings are not configured.");
            }

            if (string.IsNullOrWhiteSpace(databaseSettings.ConnectionString))
            {
                throw new InvalidOperationException("DatabaseSettings.ConnectionString is not configured.");
            }

            if (string.IsNullOrWhiteSpace(databaseSettings.DBProvider))
            {
                throw new InvalidOperationException("DatabaseSettings.DBProvider is not configured.");
            }

            return databaseSettings;
        }
    }
}
