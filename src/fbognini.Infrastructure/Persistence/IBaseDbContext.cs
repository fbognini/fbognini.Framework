using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Persistence
{
    public interface IBaseDbContext : IMultiTenantDbContext
    {
        DbSet<Audit> AuditTrails { get; set; }
        DbSet<OutboxMessage> OutboxMessages { get; set; }

        public string? UserId { get; }
        public DateTime Timestamp { get; }
        public string? Tenant { get; }
        public string? ConnectionString { get; }
        public string DBProvider { get; }

        Task<int> BaseSaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
