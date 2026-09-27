using fbognini.Infrastructure.Entities;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Multitenancy
{
    // Finbuckle 10 renamed the store operations and gave the multi-tenant context an init-only shape.
    // These shims keep both supported majors behind a single call site.
    internal static class MultitenancyCompat
    {
        public static Task<TTenant?> GetTenantAsync<TTenant>(this IMultiTenantStore<TTenant> store, string id)
            where TTenant : Tenant, new()
#if NET10_0_OR_GREATER
            => store.GetAsync(id);
#else
            => store.TryGetAsync(id);
#endif

        public static Task<bool> AddTenantAsync<TTenant>(this IMultiTenantStore<TTenant> store, TTenant tenant)
            where TTenant : Tenant, new()
#if NET10_0_OR_GREATER
            => store.AddAsync(tenant);
#else
            => store.TryAddAsync(tenant);
#endif

        public static Task<bool> UpdateTenantAsync<TTenant>(this IMultiTenantStore<TTenant> store, TTenant tenant)
            where TTenant : Tenant, new()
#if NET10_0_OR_GREATER
            => store.UpdateAsync(tenant);
#else
            => store.TryUpdateAsync(tenant);
#endif

        public static Task<bool> RemoveTenantAsync<TTenant>(this IMultiTenantStore<TTenant> store, string identifier)
            where TTenant : Tenant, new()
#if NET10_0_OR_GREATER
            => store.RemoveAsync(identifier);
#else
            => store.TryRemoveAsync(identifier);
#endif

        public static void SetCurrentTenant<TTenant>(this IMultiTenantContextSetter setter, TTenant? tenant)
            where TTenant : Tenant, new()
        {
#if NET10_0_OR_GREATER
            // A null tenant is legitimate here and yields an unresolved context, same as on Finbuckle 9.
            setter.MultiTenantContext = new MultiTenantContext<TTenant>(tenant!);
#else
            setter.MultiTenantContext = new MultiTenantContext<TTenant>
            {
                TenantInfo = tenant
            };
#endif
        }
    }
}
