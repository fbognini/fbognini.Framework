using System.Data.Common;
using fbognini.Infrastructure.Tests.Integration.Fixture;
using fbognini.Infrastructure.Tests.Integration.Fixture.Entities.Seeds;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace fbognini.Infrastructure.Tests.Integration;

public class AuditTrailTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture fixture;

    public AuditTrailTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private sealed class SelectCounter : DbCommandInterceptor
    {
        public int Selects { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                Selects++;
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<int> SeedAuthorAsync(string lastName)
    {
        await using var context = fixture.DbContext;
        var author = AuthorSeed.Faker.Generate();
        author.LastName = lastName;
        context.Authors.Add(author);
        await context.SaveChangesAsync();
        return author.Id;
    }

    [Fact]
    public async Task SaveChangesAsync_DoesNotReadBackRows_WhenRowsAreAdded()
    {
        var counter = new SelectCounter();
        await using var context = fixture.DbContextWith(counter);
        context.Authors.AddRange(AuthorSeed.Faker.Generate(5));

        await context.SaveChangesAsync();

        counter.Selects.Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditsOldValuesWithoutReadingThemBack_WhenTrackedRowsAreDeleted()
    {
        var ids = new[] { await SeedAuthorAsync("Deleted-1"), await SeedAuthorAsync("Deleted-2"), await SeedAuthorAsync("Deleted-3") };
        var counter = new SelectCounter();
        await using var context = fixture.DbContextWith(counter);
        var authors = await context.Authors.Where(x => ids.Contains(x.Id)).ToListAsync();
        var selectsBeforeSave = counter.Selects;

        context.Authors.RemoveRange(authors);
        await context.SaveChangesAsync();

        counter.Selects.Should().Be(selectsBeforeSave);
        var audits = await context.AuditTrails.Where(x => x.Type == "Delete" && x.TableName == "Author").ToListAsync();
        audits.Where(x => x.OldValues!.Contains("Deleted-")).Should().HaveCount(3);
    }

    // An entity attached with Update() carries its new values as originals too: only the database knows what changed.
    [Fact]
    public async Task SaveChangesAsync_ReadsTheOldValuesFromTheDatabase_WhenADetachedRowIsUpdated()
    {
        var id = await SeedAuthorAsync("Before");

        await using (var context = fixture.DbContext)
        {
            var detached = await context.Authors.AsNoTracking().SingleAsync(x => x.Id == id);
            detached.LastName = "After";
            context.Authors.Update(detached);
            await context.SaveChangesAsync();
        }

        await using var check = fixture.DbContext;
        var audit = await check.AuditTrails.Where(x => x.Type == "Update" && x.TableName == "Author" && x.NewValues!.Contains("After")).SingleAsync();
        audit.OldValues.Should().Contain("Before");
        audit.NewValues.Should().Contain("After");
        audit.AffectedColumns.Should().Contain("LastName");
    }
}
