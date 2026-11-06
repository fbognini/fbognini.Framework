using System.Collections.Concurrent;
using fbognini.Application.Multitenancy;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence;
using fbognini.Infrastructure.Tests.Integration.Fixture;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace fbognini.Infrastructure.Tests.Integration.Outbox;

// Several applications sharing one database each run a processor on the same outbox table.
public class OutboxConcurrentReservationTests : IClassFixture<DatabaseFixture>
{
    private const string Application = "Shop.Tests";
    private const int Messages = 600;
    private const int Processors = 4;

    private readonly DatabaseFixture fixture;

    public OutboxConcurrentReservationTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
        LinqToDBForEFTools.Initialize();
    }

    [Fact]
    public async Task ConcurrentProcessors_PublishEveryMessageOnce_WithoutDeadlocks()
    {
        await SeedAsync();
        var published = new ConcurrentBag<Guid>();

        var processors = Enumerable.Range(0, Processors).Select(_ => Processor(published)).ToList();
        await Task.WhenAll(processors.Select(processor => Task.Run(() => processor.ProcessDomainEvents(CancellationToken.None))));

        Assert.Equal(Messages, published.Count);
        Assert.Equal(Messages, published.Distinct().Count());

        using var dbContext = fixture.DbContext;
        Assert.Equal(0, await dbContext.OutboxMessages.IgnoreQueryFilters().CountAsync(x => x.Application == Application && x.ProcessedOnUtc == null));
    }

    private async Task SeedAsync()
    {
        using var dbContext = fixture.DbContext;
        var start = DateTime.UtcNow.AddMinutes(-10);
        dbContext.OutboxMessages.AddRange(Enumerable.Range(0, Messages).Select(i => new OutboxMessage(Guid.NewGuid(), start.AddMilliseconds(i), Application, "Event", "{}")));
        await dbContext.SaveChangesAsync();
    }

    private IOutboxMessageProcessor Processor(ConcurrentBag<Guid> published)
    {
        var publisher = Substitute.For<IOutboxMessagePublisher>();
        publisher.Publish(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                published.Add(call.Arg<OutboxMessage>().Id);
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(publisher);
        services.AddMultiTenant<Tenant>();
        var provider = services.BuildServiceProvider();

        return new OutboxMessageProcessor<IntegrationTestsDbContext, Tenant>(
            publisher,
            NullLogger<OutboxMessageProcessor<IntegrationTestsDbContext, Tenant>>.Instance,
            provider,
            fixture.DbContextFactory,
            Substitute.For<ITenantService<Tenant>>(),
            Microsoft.Extensions.Options.Options.Create(new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.All, BatchSize = 20 }),
            Microsoft.Extensions.Options.Options.Create(new DatabaseSettings { DBProvider = "mssql" }));
    }
}
