using fbognini.Core.Interfaces;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence;
using fbognini.Infrastructure.Repository;
using fbognini.Infrastructure.Tests.Integration.Fixture;
using fbognini.Infrastructure.Tests.Integration.Fixture.Entities;
using fbognini.Infrastructure.Tests.Integration.Fixture.Entities.Seeds;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace fbognini.Infrastructure.Tests.Integration;

public class RepositoryOwnedScopeTests : IClassFixture<FullDatabaseFixture>
{
    private readonly FullDatabaseFixture databaseFixture;

    public RepositoryOwnedScopeTests(FullDatabaseFixture databaseFixture)
    {
        this.databaseFixture = databaseFixture;
    }

    [Fact]
    public void FactoryConstructor_Throws_WhenTheScopeOfTheFactoryIsDisposed()
    {
        using var provider = BuildProvider();

        IDbContextFactory<IntegrationTestsDbContext> factory;
        using (var scope = provider.CreateScope())
        {
            factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<IntegrationTestsDbContext>>();
        }

        var act = () => new RepositoryAsync<IntegrationTestsDbContext>(factory, Substitute.For<ILogger<RepositoryAsync<IntegrationTestsDbContext>>>());

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task OwnedScopeConstructor_Reads_WhenTheScopeOfTheCallerIsDisposed()
    {
        using var provider = BuildProvider();

        var callerScope = provider.CreateScope();
        var scopeFactory = callerScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        callerScope.Dispose();

        using var repository = new OwnedScopeRepository(scopeFactory);

        var authors = await repository.GetAllAsync<Author>();

        authors.Count.Should().Be(AuthorSeed.Total);
    }

    [Fact]
    public void Dispose_DisposesTheOwnedScope()
    {
        using var provider = BuildProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var repository = new OwnedScopeRepository(scopeFactory);
        var currentUserService = provider.GetRequiredService<List<DisposableCurrentUserService>>().Should().ContainSingle().Subject;
        currentUserService.IsDisposed.Should().BeFalse();

        repository.Dispose();

        currentUserService.IsDisposed.Should().BeTrue();
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContextFactory<IntegrationTestsDbContext>(
            options => options.UseSqlServer(databaseFixture.ConnectionString),
            ServiceLifetime.Scoped);

        services.AddSingleton(Substitute.For<IOptions<DatabaseSettings>>());
        services.AddSingleton<IDateTimeProvider, IntegrationTestsDateTimeProvider>();
        services.AddSingleton(Substitute.For<IOutboxMessagesListener>());
        services.AddScoped<ITenantInfo>(_ => new TenantInfo());
        services.AddSingleton<List<DisposableCurrentUserService>>();
        services.AddScoped<ICurrentUserService>(sp =>
        {
            var currentUserService = new DisposableCurrentUserService();
            sp.GetRequiredService<List<DisposableCurrentUserService>>().Add(currentUserService);
            return currentUserService;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private class OwnedScopeRepository : RepositoryAsync<IntegrationTestsDbContext>
    {
        public OwnedScopeRepository(IServiceScopeFactory scopeFactory)
            : base(scopeFactory, Substitute.For<ILogger<RepositoryAsync<IntegrationTestsDbContext>>>())
        {
        }
    }

    public class DisposableCurrentUserService : IntegrationTestsCurrentUserService, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
