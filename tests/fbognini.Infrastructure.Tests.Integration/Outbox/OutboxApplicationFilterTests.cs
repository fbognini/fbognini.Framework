using fbognini.Infrastructure.Outbox;

namespace fbognini.Infrastructure.Tests.Integration.Outbox;

public class OutboxApplicationFilterTests
{
    private static readonly IQueryable<OutboxMessage> Messages = new[]
    {
        Message("Shop.Dashboard"),
        Message("Shop.WebApi"),
        Message("Shop.Console"),
    }.AsQueryable();

    private static OutboxMessage Message(string application) => new(Guid.NewGuid(), DateTime.UtcNow, application, "Event", "{}");

    private static string[] Filter(OutboxSettings settings)
        => OutboxApplicationFilter.Apply(Messages, settings, "Shop.Dashboard").Select(x => x.Application).ToArray();

    [Fact]
    public void Me_KeepsOnlyTheMessagesOfThisApplication()
    {
        Assert.Equal(["Shop.Dashboard"], Filter(new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.Me }));
    }

    [Fact]
    public void Custom_KeepsTheListedApplications()
    {
        var settings = new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.Custom, Applications = "Shop.Dashboard; Shop.Console" };

        Assert.Equal(["Shop.Dashboard", "Shop.Console"], Filter(settings));
    }

    [Fact]
    public void CustomWithoutApplications_KeepsNothing()
    {
        Assert.Empty(Filter(new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.Custom }));
    }

    [Fact]
    public void All_KeepsEveryMessage()
    {
        Assert.Equal(3, Filter(new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.All }).Length);
    }

    [Fact]
    public void None_KeepsNothing()
    {
        Assert.Empty(Filter(new OutboxSettings { ApplicationFilter = OutboxProcessorApplicationFilter.None }));
    }
}
