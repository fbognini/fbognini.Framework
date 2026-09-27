using fbognini.Core.Exceptions;
using fbognini.Core.Interfaces;
using fbognini.Infrastructure.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Multitenancy
{
    public class TenantGuardMiddleware : IMiddleware
    {
        private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;
        private readonly MultitenancySettings _multitenancySettings;
        private readonly IDateTimeProvider _dateTimeProvider;

        public TenantGuardMiddleware(IMultiTenantContextAccessor multiTenantContextAccessor, IOptions<MultitenancySettings> options, IDateTimeProvider dateTimeProvider)
        {
            _multiTenantContextAccessor = multiTenantContextAccessor;
            _multitenancySettings = options.Value;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var tenant = _multiTenantContextAccessor.MultiTenantContext.TenantInfo as Tenant;

            if ((_multitenancySettings.IncludeAll && (StartWithPaths(context, _multitenancySettings.IncludePaths) || !StartWithPaths(context, _multitenancySettings.ExcludePaths)))
                || StartWithPaths(context, _multitenancySettings.IncludePaths))
            {
                if (tenant == null)
                {
                    throw new IdentityException("Tenant is not provided or it doesn't exist.", "Tenant authentication failed.");
                }

                if (tenant.Identifier != MultitenancyConstants.Root.Key)
                {
                    if (!tenant.IsActive)
                    {
                        throw new IdentityException("Tenant is not active. Please contact the Administrator.", "Tenant is not active.");
                    }

                    if (_dateTimeProvider.UtcNow > tenant.ValidUpto)
                    {
                        throw new IdentityException($"Tenant validity has expired on {tenant.ValidUpto:O}. Please contact the Administrator.", "Tenant validity has expired.");
                    }
                }
            }

            await next(context);
        }

        private static bool StartWithPaths(HttpContext context, List<string> paths)
        {
            if (paths == null)
                return false;

            foreach (string item in paths)
            {
                if (context.Request.Path.StartsWithSegments(item))
                {
                    return true;
                }
            }

            return false;
        }
    }

}