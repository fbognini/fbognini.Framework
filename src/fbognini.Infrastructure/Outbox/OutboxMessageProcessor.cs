using EFCore.BulkExtensions;
using fbognini.Application.Multitenancy;
using fbognini.Infrastructure.Common;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Multitenancy;
using fbognini.Infrastructure.Persistence;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Outbox
{
    public interface IOutboxMessageProcessor
    {
        Task<int> ProcessDomainEvents(CancellationToken cancellationToken);
        Task<int> ProcessDomainEvents(bool ignoreApplicationFiter, CancellationToken cancellationToken);
    }

    internal class OutboxMessageProcessor<TDbContext, TTenant>: IOutboxMessageProcessor
        where TDbContext : DbContext, IBaseDbContext
        where TTenant : Tenant, new()
    {
        private readonly ILogger<OutboxMessageProcessor<TDbContext, TTenant>> logger;

        private readonly IServiceProvider serviceProvider;
        private readonly IDbContextFactory<TDbContext> dbContextFactory;
        private readonly ITenantService<TTenant> tenantService;
        private readonly OutboxSettings outboxSettings;
        private readonly DatabaseSettings databaseSettings;

        public OutboxMessageProcessor(
            // I'll use it in a different scope. Throw exception if not in DI.
            IOutboxMessagePublisher _, 
            ILogger<OutboxMessageProcessor<TDbContext, TTenant>> logger,
            IServiceProvider serviceProvider,
            IDbContextFactory<TDbContext> dbContextFactory,
            ITenantService<TTenant> tenantService,
            IOptions<OutboxSettings> outboxSettings,
            IOptions<DatabaseSettings> databaseSettings)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.dbContextFactory = dbContextFactory;
            this.tenantService = tenantService;
            this.outboxSettings = outboxSettings.Value;
            this.databaseSettings = databaseSettings.Value;
        }

        public Task<int> ProcessDomainEvents(CancellationToken cancellationToken) => ProcessDomainEvents(false, cancellationToken);

        public async Task<int> ProcessDomainEvents(bool ignoreApplicationFiter, CancellationToken cancellationToken)
        {
            int noOfOutboxMessages;
            do
            {
                using var dbContext = dbContextFactory.CreateDbContext();

                var outboxMessages = await ReserveOutboxMessages(dbContext, DateTime.UtcNow, ignoreApplicationFiter, cancellationToken);
                noOfOutboxMessages = outboxMessages.Count;

                logger.LogInformation("{NoOfOutboxMessages} outbox message(s) found", noOfOutboxMessages);

                foreach (var outboxMessage in outboxMessages)
                {
                    var propertys = new Dictionary<string, object?>()
                    {
                        ["OutboxMessageId"] = outboxMessage.Id,
                        ["Tenant"] = outboxMessage.TenantId,
                    };

                    using (logger.BeginScope(propertys))
                    {
                        await Process(outboxMessage, cancellationToken);

                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            while (noOfOutboxMessages > 0);

            return noOfOutboxMessages;
        }


        private async Task Process(OutboxMessage outboxMessage, CancellationToken cancellationToken)
        {
            var outboxMessageId = outboxMessage.Id;
            var outboxTenant = outboxMessage.TenantId;

            try
            {
                var tenant = await GetTenant(outboxTenant);
                if (tenant is null && !string.IsNullOrWhiteSpace(outboxTenant))
                {
                    logger.LogWarning("Tenat {Tenant} for outbox message {OutboxMessageId} was not found", outboxTenant, outboxMessageId);
                }

                serviceProvider.GetRequiredService<IMultiTenantContextSetter>().SetCurrentTenant(tenant);

                using (var outboxScope = serviceProvider.CreateScope())
                {
                    var publisher = outboxScope.ServiceProvider.GetRequiredService<IOutboxMessagePublisher>();

                    logger.LogDebug("Start processing outbox message {OutboxMessageId} as {OutboxMessageType}", outboxMessageId, outboxMessage.Type);

                    await publisher.Publish(outboxMessage, cancellationToken);
                }

                outboxMessage.SetAsProcessed(DateTime.UtcNow);

                logger.LogInformation("Outbox message {OutboxMessageId} has been processed", outboxMessageId);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Exception while processing outbox message {OutboxMessageId}",
                    outboxMessage.Id);

                outboxMessage.SetAsProcessedWithError(DateTime.UtcNow, exception.Message);
            }
        }

        private async Task<TTenant?> GetTenant(string outboxTenant)
        {
            if (string.IsNullOrWhiteSpace(outboxTenant))
            {
                return null;
            }

            return await tenantService.GetByIdAsync(outboxTenant);
        }

        private async Task<List<OutboxMessage>> ReserveOutboxMessages(TDbContext dbContext, DateTime utc, bool ignoreApplicationFiter, CancellationToken cancellationToken)
        {
            using var transaction = dbContext.Database.BeginTransaction();

            var applicationName = Assembly.GetEntryAssembly()!.GetName().Name!;

            var linq2dbTable = dbContext.OutboxMessages
                .ToLinqToDBTable();

            if (databaseSettings.DBProvider == DbProviderKeys.SqlServer)
            {
                //linq2dbTable = linq2dbTable
                //        .TableHint(SqlServerHints.Table.UpdLock)
                //        .TableHint(SqlServerHints.Table.RowLock)
                //        .TableHint(SqlServerHints.Table.ReadPast);

                linq2dbTable = linq2dbTable
                        .TableHint(SqlServerHints.Table.UpdLock)
                        .TableHint(SqlServerHints.Table.ReadPast);
            }

            var query = linq2dbTable
                // GlobalQueryFilter for tenant
                .IgnoreFilters()
                .Where(x => !x.ProcessedOnUtc.HasValue &&
                    (!x.IsProcessing || x.IsProcessing && x.ExpiredOnUtc < utc));

            if (databaseSettings.DBProvider == DbProviderKeys.Npgsql)
            {
                query = query.QueryHint(PostgreSQLHints.ForUpdate);
            }

            if (ignoreApplicationFiter)
            {
                if (outboxSettings.ApplicationFilter == OutboxProcessorApplicationFilter.Me)
                {
                    query = query.Where(x => x.Application == applicationName);
                }
                else if (outboxSettings.ApplicationFilter == OutboxProcessorApplicationFilter.Custom &&
                    outboxSettings.Applications is not null)
                {
                    var applications = outboxSettings.Applications.Split(new[] { ',', ';' }).ToList();
                    query = query.Where(x => applications.Contains(x.Application));
                }
            }

            query = query
                .OrderBy(o => o.OccurredOnUtc)
                .Take(outboxSettings.BatchSize);

            var events = query.ToList();
            if (events.Count > 0)
            {
                var lockId = GetUuidV7(utc);
                var expiredOnUtc = utc.AddMinutes(5);

                foreach (var @event in events)
                {
                    @event.IsProcessing = true;
                    @event.LockId = lockId;
                    @event.ReservedOnUtc = utc;
                    @event.ExpiredOnUtc = expiredOnUtc;
                }

                await dbContext.BulkUpdateAsync(events, cancellationToken: cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return events;

//#if NET6_0
//            throw new NotImplementedException("NET. 6 not supported but outbox processing");
//#else

//            query.ExecuteUpdate(setters => setters
//                    .SetProperty(b => b.IsProcessing, true)
//                    .SetProperty(b => b.LockId, lockId)
//                    .SetProperty(b => b.ReservedOnUtc, utc)
//                    .SetProperty(b => b.ExpiredOnUtc, expiredOnUtc));

//            return await dbContext.OutboxMessages
//                .AsNoTracking()
//                .Where(x => x.LockId == lockId)
//                .ToListAsyncEF(cancellationToken);
//#endif

        }



        public Guid GetUuidV7(DateTimeOffset dateTimeOffset)
        {
            // bytes [0-5]: datetimeoffset yyyy-MM-dd hh:mm:ss fff
            // bytes [6]: 4 bits dedicated to guid version (version: 7)
            // bytes [6]: 4 bits dedicated to random part
            // bytes [7-15]: random part
            byte[] uuidAsBytes = new byte[16];
            FillTimePart(ref uuidAsBytes, dateTimeOffset);
            Span<byte> random_part = uuidAsBytes.AsSpan().Slice(6);
            RandomNumberGenerator.Fill(random_part);
            // add mask to set guid version
            uuidAsBytes[6] &= 0x0F;
            uuidAsBytes[6] += 0x70;
            var guid = new Guid(uuidAsBytes);
            return guid;
        }

        private void FillTimePart(ref byte[] uuidAsBytes, DateTimeOffset dateTimeOffset)
        {
            long currentTimestamp = dateTimeOffset.ToUnixTimeMilliseconds();
            byte[] current = BitConverter.GetBytes(currentTimestamp);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(current);
            }
            current[2..8].CopyTo(uuidAsBytes, 0);
        }

    }
}
