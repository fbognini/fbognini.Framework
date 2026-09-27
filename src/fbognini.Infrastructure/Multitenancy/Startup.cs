using fbognini.Application.Multitenancy;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Multitenancy
{
    public static class Startup
    {
        private static MultiTenantBuilder<TTenant> AddBaseMultitenancy<TTenant>(this IServiceCollection services, IConfiguration configuration)
            where TTenant : Tenant, new()
        {
            return services
                .Configure<MultitenancySettings>(configuration.GetSection(nameof(MultitenancySettings)))
                .AddScoped<ITenantService<TTenant>, TenantService<TTenant>>()
                .AddMultiTenant<TTenant>();
        }

        public static MultiTenantBuilder<TTenant> AddMultitenancy<TTenantContext, TTenant>(this IServiceCollection services, IConfiguration configuration)
            where TTenantContext: TenantDbContext<TTenant>
            where TTenant : Tenant, new()
        {
            var builder = services
                    .AddBaseMultitenancy<TTenant>(configuration)
                    .WithEFCoreStore<TTenantContext, TTenant>();

            return builder;
        }

        public static MultiTenantBuilder<TTenant> AddMultitenancy<TTenant>(this IServiceCollection services, IConfiguration configuration)
            where TTenant : Tenant, new()
        {
            return services
                    .AddBaseMultitenancy<TTenant>(configuration);
        }

        public static MultiTenantBuilder<Tenant> AddMultitenancy(this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddMultitenancy<Tenant>(configuration);
        }

        public static MultiTenantBuilder<TTenant> WithFakeMultitenancy<TTenant>(this MultiTenantBuilder<TTenant> builder)
            where TTenant : Tenant, new()
        {
            return builder
                .WithInMemoryStore(options =>
                {
                    options.IsCaseSensitive = true;
                    options.Tenants.Add(new TTenant
                    {
                        Identifier = MultitenancyConstants.Root.Key,
                        Name = MultitenancyConstants.Root.Name,
                        ConnectionString = string.Empty,
                        AdminEmail = MultitenancyConstants.Root.EmailAddress,
                        IsActive = true,
                        Issuer = null,
                        ValidUpto = DateTime.UtcNow.AddYears(1)
                    });
                })
                .WithStaticStrategy(MultitenancyConstants.Root.Key);
        }

        public static MultiTenantBuilder<Tenant> WithFakeMultitenancy(this MultiTenantBuilder<Tenant> builder)
        {
            return builder.WithFakeMultitenancy<Tenant>();
        }

        public static MultiTenantBuilder<Tenant> WithQueryStringStrategy(this MultiTenantBuilder<Tenant> builder, string queryStringKey) =>
            builder.WithDelegateStrategy(context =>
            {
                if (context is HttpContext httpContext)
                {
                    httpContext.Request.Query.TryGetValue(queryStringKey, out StringValues tenantIdParam);

                    return Task.FromResult((string?)tenantIdParam.ToString());
                }

                return Task.FromResult((string?)null);
            });

        public static MultiTenantBuilder<Tenant> WithOriginOrRefererStrategy(this MultiTenantBuilder<Tenant> builder) =>
            builder.WithDelegateStrategy(context =>
            {
                if (context is HttpContext httpContext)
                {
                    return Task.FromResult(ResolveFromOriginOrReferer(httpContext));
                }

                return Task.FromResult((string?)null);
            });



        public static async Task InitializeMultiTenancyAsync(this IApplicationBuilder app, CancellationToken cancellationToken = default)
        {
            using var serviceScope = app.ApplicationServices.CreateScope();

            var initializers = serviceScope.ServiceProvider.GetServices<IMultiTenantDatabaseInitializer>();
            foreach (var initializer in initializers)
            {
                await initializer.InitializeDatabasesAsync(cancellationToken);
            }
        }

        [Obsolete("Please use app.InitializeMultiTenancy, app.UseMultiTenant and app.UseMiddleware<TenantGuardMiddleware> as needed")]
        public static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app)
        {
            using var serviceScope = app.ApplicationServices.CreateScope();

            var initializers = serviceScope.ServiceProvider.GetServices<IMultiTenantDatabaseInitializer>();
            foreach (var initializer in initializers)
            {
                initializer.InitializeDatabasesAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            app.UseMultiTenant();

            return app;
        }


        private static string? ResolveFromOriginOrReferer(HttpContext context)
        {
            var origins = context.Request.Headers["origin"];
            if (origins.Count > 0 && origins[0] != "null")
                return ExtractTenantFromUrl(origins[0]);

            var referers = context.Request.Headers["referer"];
            if (referers.Count > 0 && referers[0] != "null")
                return ExtractTenantFromUrl(referers[0]);

            return null;
        }

        static string? ExtractTenantFromUrl(string origin)
        {
            if (origin.Contains("http://"))
                origin = origin.Replace("http://", "");

            if (origin.Contains("https://"))
                origin = origin.Replace("https://", "");

            if (origin.StartsWith("localhost"))
                return null;


            var splits = origin.Split('.');
            if (splits.Length != 0)
            {
                return splits[0];
            }

            return origin;
        }
    }
}
