using fbognini.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace fbognini.Infrastructure.Multitenancy
{
    public class TenantDbContext<TTenant> : EFCoreStoreDbContext<TTenant>
        where TTenant : Tenant, new()
    {
        public TenantDbContext(DbContextOptions<TenantDbContext<TTenant>> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TTenant>().ToTable("Tenants", "multitenancy");
        }
    }
}
