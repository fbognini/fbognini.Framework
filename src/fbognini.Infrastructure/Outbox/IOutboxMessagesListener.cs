using fbognini.Application.Multitenancy;
using fbognini.Core.Domain;
using fbognini.Infrastructure.Entities;
using fbognini.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Outbox;

public interface IOutboxMessagesListener
{
    Task PublishDomainMemoryEventAsync(IReadOnlyList<IDomainMemoryEvent> domainMemoryEvents, CancellationToken cancellationToken);

    void NotifyDomainEvents();
}

internal class OutboxMessagesListenerService : BackgroundService, IOutboxMessagesListener
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxMessagesListenerService> logger;

    private CancellationTokenSource _wakeupDomainEventsCancellationTokenSource = new();

    public OutboxMessagesListenerService(IServiceScopeFactory scopeFactory, ILogger<OutboxMessagesListenerService> logger)
    {
        _scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int interval = -1;

        using (var scope = _scopeFactory.CreateScope())
        {
            var applicationName = Assembly.GetEntryAssembly()!.GetName().Name!;

            var outboxSettings = scope.ServiceProvider.GetRequiredService<IOptions<OutboxSettings>>().Value;
            if (outboxSettings.ApplicationFilter == OutboxProcessorApplicationFilter.None)
            {
                logger.LogInformation("Outbox messages won't be processed by {ApplicationName}", applicationName);
                return;
            }

            var publisher = scope.ServiceProvider.GetService<IOutboxMessagePublisher>();
            if (publisher is null)
            {
                logger.LogWarning("No IOutboxMessagePublisher has been registered, outbox messages won't be processed by {ApplicationName}", applicationName);
                return;
            }

            if (outboxSettings.IntervalInSeconds > 0)
            {
                interval = outboxSettings.IntervalInSeconds * 1000;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDomainEventsAndWait(interval, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during domain events processing");

                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public async Task PublishDomainMemoryEventAsync(IReadOnlyList<IDomainMemoryEvent> domainMemoryEvents, CancellationToken cancellationToken)
    {
        if (domainMemoryEvents is null || domainMemoryEvents.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetService<IOutboxMessagePublisher>();

        foreach (var domainMemoryEvent in domainMemoryEvents)
        {
            await publisher.Publish(domainMemoryEvent, cancellationToken);
        }
    }

    public void NotifyDomainEvents()
    {
        logger.LogDebug("Listener has been notified for domain events");

        _wakeupDomainEventsCancellationTokenSource.Cancel();
    }

    private async Task ProcessDomainEvents(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();

        _ = await processor.ProcessDomainEvents(cancellationToken);
    }

    private async Task ProcessDomainEventsAndWait(int interval, CancellationToken cancellationToken)
    {
        await ProcessDomainEvents(cancellationToken);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_wakeupDomainEventsCancellationTokenSource.Token, cancellationToken);
        try
        {
            if (interval == -1)
            {
                logger.LogInformation("Listening for a new event");
            }
            else
            {
                logger.LogInformation("Listening for a new event of waiting for {Internval}ms", interval);
            }

            await Task.Delay(interval, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (_wakeupDomainEventsCancellationTokenSource.Token.IsCancellationRequested)
            {
                logger.LogInformation("Outbox listener is awake");

                var tmp = _wakeupDomainEventsCancellationTokenSource;
                _wakeupDomainEventsCancellationTokenSource = new CancellationTokenSource();
                tmp.Dispose();
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Outbox listener is shutting down");
            }
        }
    }

}