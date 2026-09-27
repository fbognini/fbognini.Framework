using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Threading;
using fbognini.Core.Interfaces;
using fbognini.Infrastructure.Entities;
using System;
using fbognini.Infrastructure.Outbox;
using Microsoft.Extensions.Options;
using fbognini.Infrastructure.Common;

namespace fbognini.Infrastructure.Persistence
{
    public class AuditableDbContext<T> : DbContext, IBaseDbContext
        where T : DbContext
    {
        protected readonly ICurrentUserService _currentUserService;
        protected readonly IDateTimeProvider _dateTimeProvider;
        protected readonly IOutboxMessagesListener _outboxListenerService;
        protected readonly IMultiTenantContextAccessor? _multiTenantContextAccessor;

        protected readonly DatabaseSettings _databaseSettings;

        public AuditableDbContext(
            DbContextOptions<T> options,
            IOptions<DatabaseSettings> databaseOptions,
            ICurrentUserService currentUserService,
            IDateTimeProvider dateTimeProvider,
            IOutboxMessagesListener outboxListenerService,
            IMultiTenantContextAccessor? multiTenantContextAccessor = null)
            : base(options)
        {
            _databaseSettings = databaseOptions.Value;
            _currentUserService = currentUserService;
            _dateTimeProvider = dateTimeProvider;
            _outboxListenerService = outboxListenerService;
            _multiTenantContextAccessor = multiTenantContextAccessor;
        }

        public DbSet<Audit> AuditTrails { get; set; } = default!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = default!;

        public string? UserId => _currentUserService.UserId;
        public DateTime Timestamp => _dateTimeProvider.UtcNow;
        public string DBProvider => _databaseSettings.DBProvider;
        public string? Tenant => CurrentTenant?.Id;
        public string? ConnectionString => CurrentTenant?.ConnectionString;

        protected fbognini.Infrastructure.Entities.Tenant? CurrentTenant =>
            _multiTenantContextAccessor?.MultiTenantContext.TenantInfo as fbognini.Infrastructure.Entities.Tenant;

        public ITenantInfo? TenantInfo => CurrentTenant;

        // Mismatches are a bug, not a recoverable state; an unset tenant is normal on insert and gets filled from the context.
        public virtual TenantMismatchMode TenantMismatchMode => TenantMismatchMode.Throw;
        public virtual TenantNotSetMode TenantNotSetMode => TenantNotSetMode.Overwrite;


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureDbProvider(this);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsAndFilters(this);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return this.AuditableSaveChangesAsync(_outboxListenerService, cancellationToken);
        }

        public Task<int> BaseSaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
