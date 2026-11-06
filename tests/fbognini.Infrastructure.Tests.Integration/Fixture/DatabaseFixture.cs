using fbognini.Core.Interfaces;
using fbognini.Infrastructure.Outbox;
using fbognini.Infrastructure.Persistence;
using fbognini.Infrastructure.Repository;
using fbognini.Infrastructure.Tests.Integration.Fixture.Entities.Seeds;
using Finbuckle.MultiTenant;
using MartinCostello.SqlLocalDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace fbognini.Infrastructure.Tests.Integration.Fixture;

public class DatabaseFixture : IDisposable
{
    private readonly IOptions<DatabaseSettings> databaseOptions = Substitute.For<IOptions<DatabaseSettings>>();
    private readonly ICurrentUserService currentUserService = new IntegrationTestsCurrentUserService();
    private readonly IDateTimeProvider dateTimeProvider = new IntegrationTestsDateTimeProvider();
    private readonly IOutboxMessagesListener outboxMessagesListener = Substitute.For<IOutboxMessagesListener>();
    private readonly ITenantInfo tenantInfo = new TenantInfo();

    public DbContextOptions<IntegrationTestsDbContext> DbContextOptions { get; }
    public string ConnectionString { get; }

    public IDbContextFactory<IntegrationTestsDbContext> DbContextFactory => 
        new IntegrationTestsDbContextFactory(DbContextOptions, databaseOptions, currentUserService, dateTimeProvider, outboxMessagesListener, tenantInfo);

    public IntegrationTestsDbContext DbContext => new (DbContextOptions, databaseOptions, currentUserService, dateTimeProvider, outboxMessagesListener, tenantInfo);

    public IntegrationTestsDbContext DbContextWith(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<IntegrationTestsDbContext>(DbContextOptions)
            .AddInterceptors(interceptors)
            .Options;

        return new(options, databaseOptions, currentUserService, dateTimeProvider, outboxMessagesListener, tenantInfo);
    }

    public RepositoryAsync<IntegrationTestsDbContext> Repository => new(DbContextFactory, Substitute.For<ILogger<RepositoryAsync<IntegrationTestsDbContext>>>());

    public DatabaseFixture()
    {
        var databaseName = $"fbognini.Infrastructure.Tests.Integration_{Guid.NewGuid().ToString().Replace('-', '_')}";

        using (var localDB = new SqlLocalDbApi())
        {
            ConnectionString = localDB.IsLocalDBInstalled()
                ? $"Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog={databaseName};Integrated Security=SSPI;MultipleActiveResultSets=True;Connection Timeout=300;Encrypt=False;TrustServerCertificate=True;"
                : throw new InvalidOperationException("LocalDB must be installed");
        }

        DbContextOptions = new DbContextOptionsBuilder<IntegrationTestsDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        using var dbContext = DbContext;
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var dbContext = DbContext;
        dbContext.Database.EnsureDeleted();
        GC.SuppressFinalize(this);
    }
}

public class FullDatabaseFixture : DatabaseFixture
{
    public FullDatabaseFixture()
        : base()
    {
        using var dbContext = DbContext;

        dbContext.Authors.AddRange(AuthorSeed.GetAuthors());
        dbContext.SaveChanges();
    }
}

public class IntegrationTestsCurrentUserService : ICurrentUserService
{
    public string UserId => "IntegrationTests";

    public string UserName => "IntegrationTests";

    public List<string> GetRoles()
    {
        throw new NotImplementedException();
    }

    public bool HasClaim(string type, string value)
    {
        throw new NotImplementedException();
    }
}

public class IntegrationTestsDateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;
}