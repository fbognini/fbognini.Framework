using fbognini.Core.Domain;
using fbognini.Infrastructure.Common;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence.ConnectionString;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Persistence
{
    public static class DbContextExtensions
    {
        public static void ConfigureDbProvider(this DbContextOptionsBuilder optionsBuilder, IBaseDbContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.ConnectionString))
            {
                optionsBuilder.ConfigureDbProvider(context.DBProvider, context.ConnectionString);
            }
        }

        public static void ConfigureDbProvider(this DbContextOptionsBuilder optionsBuilder, string dbProvider, string connectionString)
        {
            switch (dbProvider)
            {
                case DbProviderKeys.SqlServer:
                    optionsBuilder.UseSqlServer(connectionString.WithApplicationName(dbProvider)!);
                    break;
                case DbProviderKeys.Npgsql:
                    optionsBuilder.UseNpgsql(connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"DBProvider {dbProvider} not supported");
            }
        }


        public static void ApplyConfigurationsAndFilters(this ModelBuilder builder, IBaseDbContext context)
        {
            builder.ApplyConfigurationsFromAssembly(context.GetType().GetTypeInfo().Assembly);

            builder.ApplyGlobalFilters<ISoftDelete>(s => s.DeletedOnUtc == null);
            builder.ApplyMultiTenant<IHaveTenant>();

            builder.Entity<OutboxMessage>(b =>
            {
                b.HasIndex(x => x.IsProcessing).HasFilter("IsProcessing = 0");
                b.HasIndex(x => x.LockId).HasFilter("IsProcessing = 1");

                b.Property(x => x.Application)
                    .HasMaxLength(200);

                b.Property(x => x.Type)
                    .HasMaxLength(500);
            });
        }

        public static void SetNewProperty(this IAuditableEntity entity, string name, string? value)
        {
            var newProperty = entity.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (newProperty != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    newProperty.SetValue(entity, null);
                }
                else
                {
                    newProperty.SetValue(entity, TypeDescriptor.GetConverter(newProperty.PropertyType).ConvertFromString(value));
                }

            }
        }


        private static void FillAuditablePropertys<TContext>(this TContext context)
            where TContext : DbContext, IBaseDbContext
        {
            foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.FillAuditablePropertysAdded(context);
                        break;
                    case EntityState.Modified:
                        entry.Entity.FillAuditablePropertysModified(context);
                        break;
                    case EntityState.Deleted:
                        if (entry.Entity is ISoftDelete softDelete)
                        {
                            entry.Entity.SetNewProperty(nameof(ISoftDelete.DeletedBy), context.UserId);
                            softDelete.DeletedBy = context.UserId;
                            softDelete.DeletedOnUtc = context.Timestamp;
                            entry.State = EntityState.Modified;
                        }

                        break;
                }
            }
        }

        private static void FillTenantProperty<TContext>(this TContext context)
            where TContext : DbContext, IBaseDbContext
        {
            if (string.IsNullOrWhiteSpace(context.Tenant))
            {
                return;
            }

            foreach (var entry in context.ChangeTracker.Entries<IHaveTenant>())
            {
                if ((entry.State == EntityState.Added || entry.State == EntityState.Modified) &&
                    string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                {
                    entry.Entity.TenantId = context.Tenant;
                }
            }
        }


        public static void FillAuditablePropertysAdded(this IAuditableEntity entity, IBaseDbContext context)
        {
            entity.SetNewProperty(nameof(IAuditableEntity.CreatedBy), context.UserId);
            entity.SetNewProperty(nameof(IAuditableEntity.LastUpdatedBy), context.UserId);
            entity.CreatedBy = context.UserId;
            entity.CreatedOnUtc = context.Timestamp;
            entity.LastUpdatedBy = context.UserId;
            entity.LastUpdatedOnUtc = context.Timestamp;
        }

        public static void FillAuditablePropertysModified(this IAuditableEntity entity, IBaseDbContext context)
        {
            entity.SetNewProperty(nameof(IAuditableEntity.LastModifiedBy), context.UserId);
            entity.SetNewProperty(nameof(IAuditableEntity.LastUpdatedBy), context.UserId);
            entity.LastModifiedBy = context.UserId;
            entity.LastModifiedOnUtc = context.Timestamp;
            entity.LastUpdatedBy = context.UserId;
            entity.LastUpdatedOnUtc = context.Timestamp;
        }

        public static async Task<int> AuditableSaveChangesAsync<TContext>(this TContext context, IOutboxMessagesListener outboxMessagesListener, CancellationToken cancellationToken = new CancellationToken())
            where TContext : DbContext, IBaseDbContext
        {
            var beforeResponse = await context.OnBeforeSaveChanges(cancellationToken);

            context.FillAuditablePropertys();
            context.FillTenantProperty();
            context.EnforceMultiTenant();

            var result = await context.BaseSaveChangesAsync(cancellationToken);
            await context.OnAfterSaveChanges(beforeResponse, outboxMessagesListener, cancellationToken);

            return result;
        }


        private class BeforeSaveChangesResponse
        {
            public bool HasDomainEvents { get; set; }
            public List<AuditEntry> MissingAuditEntrys { get; set; } = new();
            public List<IDomainPreEvent> DomainPreEvents { get; set; } = new();
            public List<IDomainMemoryEvent> DomainMemoryEvents { get; set; } = new();
        }


        private static async Task<BeforeSaveChangesResponse> OnBeforeSaveChanges<TContext>(this TContext context, CancellationToken cancellationToken)
            where TContext : DbContext, IBaseDbContext
        {
            var response = new BeforeSaveChangesResponse();
                
            if (!string.IsNullOrWhiteSpace(context.UserId))
            {
                var auditEntrys = await context.AddAuditTrails(cancellationToken);
                response.MissingAuditEntrys = auditEntrys.Where(_ => _.HasTemporaryProperties).ToList();
            }

            var outboxMessages = context.AddDomainEventsAsOutboxMessages();
            response.HasDomainEvents = outboxMessages.Any(_ => _.DomainEvent is not null);
            response.DomainPreEvents = outboxMessages.Where(x => x.DomainPreEvent is not null).Select(x => x.DomainPreEvent!).ToList();
            response.DomainMemoryEvents = outboxMessages.Where(x => x.DomainMemoryEvent is not null).Select(x => x.DomainMemoryEvent!).ToList();

            return response;
        }

        private static async Task OnAfterSaveChanges<TContext>(this TContext context, BeforeSaveChangesResponse beforeResponse, IOutboxMessagesListener outboxMessagesListener, CancellationToken cancellationToken)
            where TContext : DbContext, IBaseDbContext
        {
            if (!string.IsNullOrWhiteSpace(context.UserId))
            {
                await context.AddAuditTrailsWithTemporaryPropertys(beforeResponse.MissingAuditEntrys, cancellationToken);
            }

            var hasDomainPreEvents = await context.AddDomainPreEventsAsOutboxMessages(beforeResponse.DomainPreEvents, cancellationToken);

            await context.BaseSaveChangesAsync(cancellationToken);

            if (hasDomainPreEvents || beforeResponse.HasDomainEvents)
            {
                outboxMessagesListener.NotifyDomainEvents();
            }

            await outboxMessagesListener.PublishDomainMemoryEventAsync(beforeResponse.DomainMemoryEvents, cancellationToken);
        }


        private static async Task<List<AuditEntry>> AddAuditTrails<TContext>(this TContext context, CancellationToken cancellationToken)
            where TContext : DbContext, IBaseDbContext
        {
            var auditEntrys = new List<AuditEntry>();

            context.ChangeTracker.DetectChanges();
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                {
                    continue;
                }

                var auditableProperties = typeof(IAuditableEntity)
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Select(p => p.Name);

                var propertys = entry.State == EntityState.Deleted
                    ? entry.Properties
                    : entry.Properties.Where(x => !auditableProperties.Contains(x.Metadata.Name));

                var originalValues = await entry.GetDatabaseValuesAsync(cancellationToken);

                var auditEntry = new AuditEntry(entry)
                {
                    UserId = context.UserId,
                    DateTime = context.Timestamp
                };
                foreach (var property in propertys)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    var propertyName = property.Metadata.Name;

                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    if (entry.State == EntityState.Added)
                    {
                        auditEntry.AuditType = AuditType.Create;
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    var originalValue = originalValues?[propertyName];

                    if (entry.State == EntityState.Deleted)
                    {
                        auditEntry.AuditType = AuditType.Delete;
                        auditEntry.OldValues[propertyName] = originalValue;
                        continue;
                    }

                    if (entry.State == EntityState.Modified)
                    {
                        if (property.IsModified && entry.Entity is ISoftDelete && property.OriginalValue == null && property.CurrentValue != null)
                        {
                            auditEntry.ChangedColumns.Add(propertyName);
                            auditEntry.AuditType = AuditType.Delete;
                            auditEntry.OldValues[propertyName] = originalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        else if (property.IsModified && originalValue?.Equals(property.CurrentValue) == false)
                        {
                            auditEntry.ChangedColumns.Add(propertyName);
                            auditEntry.AuditType = AuditType.Update;
                            auditEntry.OldValues[propertyName] = originalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }

                        continue;
                    }
                }

                if (auditEntry.HasProperties)
                {
                    auditEntrys.Add(auditEntry);
                }
            }
            foreach (var auditEntry in auditEntrys.Where(_ => !_.HasTemporaryProperties))
            {
                context.AuditTrails.Add(auditEntry.ToAudit());
            }
            return auditEntrys;
        }

        private static List<OutboxMessageEntry> AddDomainEventsAsOutboxMessages<TContext>(this TContext context)
            where TContext : DbContext, IBaseDbContext
        {
            var outboxMessages = context.ChangeTracker
                .Entries<Entity>()
                .Select(entry => entry.Entity)
                .SelectMany(entity =>
                {
                    IReadOnlyList<IDomainEvent> domainEvents = entity.GetDomainEvents();
                    IReadOnlyList<IDomainPreEvent> domainPreEvents = entity.GetDomainPreEvents();
                    IReadOnlyList<IDomainMemoryEvent> domainMemoryEvents = entity.GetDomainMemoryEvents();

                    entity.ClearDomainEvents();
                    entity.ClearDomainPreEvents();
                    entity.ClearDomainMemoryEvents();

                    var outboxMessageEntrys = new List<OutboxMessageEntry>();
                    outboxMessageEntrys.AddRange(domainEvents.Select(domainEvent => OutboxMessageEntry.FromDomainEvent(domainEvent)));
                    outboxMessageEntrys.AddRange(domainPreEvents.Select(domainPreEvent => OutboxMessageEntry.FromDomainPreEvent(domainPreEvent)));
                    outboxMessageEntrys.AddRange(domainMemoryEvents.Select(domainMemoryEvent => OutboxMessageEntry.FromDomainMemoryEvent(domainMemoryEvent)));

                    return outboxMessageEntrys;
                })
                .ToList();

            foreach (var domainEvent in outboxMessages.Where(_ => _.DomainEvent is not null).Select(x => x.DomainEvent!))
            {
                context.OutboxMessages.Add(domainEvent.ToOutboxMessage());
            }

            return outboxMessages;
        }


        private static Task AddAuditTrailsWithTemporaryPropertys<TContext>(this TContext context, List<AuditEntry> auditEntries, CancellationToken cancellationToken)
            where TContext : DbContext, IBaseDbContext
        {
            if (auditEntries == null || auditEntries.Count == 0)
            {
                return Task.CompletedTask;
            }

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                context.AuditTrails.Add(auditEntry.ToAudit());
            }

            return Task.CompletedTask;
        }


        private static Task<bool> AddDomainPreEventsAsOutboxMessages<TContext>(this TContext context, List<IDomainPreEvent> domainPreEvents, CancellationToken cancellationToken)
            where TContext : DbContext, IBaseDbContext
        {
            if (domainPreEvents == null || domainPreEvents.Count == 0)
            {
                return Task.FromResult(false);
            }

            foreach (var domainPreEvent in domainPreEvents)
            {
                var domainEvent = domainPreEvent.ToDomainEvent();
                context.OutboxMessages.Add(domainEvent.ToOutboxMessage());
            }

            return Task.FromResult(true);
        }
    }
}
