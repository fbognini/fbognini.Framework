using fbognini.Application.Multitenancy;
using fbognini.Core.Exceptions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using fbognini.Infrastructure.Persistence.Initialization;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Persistence.ConnectionString;
using fbognini.Infrastructure.Persistence;

namespace fbognini.Infrastructure.Multitenancy
{
    public class TenantService<TTenant> : ITenantService<TTenant>
            where TTenant : Tenant, new()
    {
        private readonly IMultiTenantStore<TTenant> tenantStore;
        private readonly IConnectionStringSecurer csSecurer;
        private readonly DatabaseSettings dbSettings;
        private readonly IMultiTenantDatabaseInitializer<TTenant>? dbInitializer;

        public TenantService(
            IMultiTenantStore<TTenant> tenantStore,
            IConnectionStringSecurer csSecurer,
            IOptions<DatabaseSettings> dbSettings,
            IMultiTenantDatabaseInitializer<TTenant>? dbInitializer = null)
        {
            this.tenantStore = tenantStore;
            this.csSecurer = csSecurer;
            this.dbSettings = dbSettings.Value;
            this.dbInitializer = dbInitializer;
        }

        public async Task<List<TTenant>> GetAllAsync()
        {
            var tenants = (await tenantStore.GetAllAsync()).ToList();
            tenants.ForEach(t => t.ConnectionString = csSecurer.MakeSecure(t.ConnectionString));
            return tenants;
        }

        public async Task<bool> ExistsWithIdAsync(string id) =>
            !(await tenantStore.GetTenantAsync(id) is null);

        public async Task<bool> ExistsWithNameAsync(string name) =>
            (await tenantStore.GetAllAsync()).Any(t => t.Name == name);

        public async Task<TTenant> GetByIdAsync(string id)
        {
            var tenant = await GetTenantInfoAsync(id);
            tenant.ConnectionString = csSecurer.MakeSecure(tenant.ConnectionString);

            return tenant;
        }

        public async Task<string> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
        {
            if (request.ConnectionString?.Trim() == dbSettings.ConnectionString?.Trim()) request.ConnectionString = string.Empty;

            var tenant = new TTenant
            {
                Identifier = request.Identifier,
                Name = request.Name,
                ConnectionString = request.ConnectionString ?? string.Empty,
                AdminEmail = request.AdminEmail,
                IsActive = true,
                Issuer = request.Issuer,
                // Add Default 1 Month Validity for all new tenants. Something like a DEMO period for tenants.
                ValidUpto = DateTime.UtcNow.AddMonths(1)
            };

            await tenantStore.AddTenantAsync(tenant);


            if (dbInitializer is not null)
            {
                // TODO: run this in a hangfire job? will then have to send mail when it's ready or not
                try
                {
                    await dbInitializer.InitializeApplicationDbForTenantAsync(tenant, cancellationToken);
                }
                catch
                {
                    await tenantStore.RemoveTenantAsync(request.Identifier);
                    throw;
                }
            }

            return tenant.Id.ToString();
        }

        public async Task<string> ActivateAsync(string id)
        {
            var tenant = await GetTenantInfoAsync(id);

            if (tenant.IsActive)
            {
                throw new ConflictException("Tenant is already Activated.");
            }

            tenant.Activate();

            await tenantStore.UpdateTenantAsync(tenant);

            return String.Format("Tenant {0} is now Activated.", id);
        }

        public async Task<string> DeactivateAsync(string id)
        {
            var tenant = await GetTenantInfoAsync(id);

            if (!tenant.IsActive)
            {
                //throw new ConflictException(_t["Tenant is already Deactivated."]);
                throw new ConflictException("Tenant is already Deactivated.");
            }

            tenant.Deactivate();

            await tenantStore.UpdateTenantAsync(tenant);

            return String.Format("Tenant {0} is now Deactivated.", id);
        }

        public async Task<string> UpdateSubscription(string id, DateTime extendedExpiryDate)
        {
            var tenant = await GetTenantInfoAsync(id);

            tenant.SetValidity(extendedExpiryDate);

            await tenantStore.UpdateTenantAsync(tenant);

            //return _t[$"Tenant {0}'s Subscription Upgraded. Now Valid till {1}.", id, tenant.ValidUpto];
            return String.Format("Tenant {0}'s Subscription Upgraded. Now Valid till {1}.", id, tenant.ValidUpto);
        }

        private async Task<TTenant> GetTenantInfoAsync(string id) =>
            await tenantStore.GetTenantAsync(id)
                ?? throw new NotFoundException(typeof(TTenant), id);
    }

}