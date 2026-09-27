using fbognini.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fbognini.Infrastructure.Outbox;

public sealed class OutboxMessage: IHaveTenant
{
    private OutboxMessage() { }
    public OutboxMessage(Guid id, DateTime occurredOnUtc, string application, string type, string content)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        Application = application;
        Content = content;
        Type = type;
    }

    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public string Application { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public bool IsProcessing { get; set; }
    public Guid? LockId { get; set; }
    public DateTime? ReservedOnUtc { get; set; }
    public DateTime? ExpiredOnUtc { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public void SetAsProcessed(DateTime processedOnUtc)
    {
        if (ProcessedOnUtc.HasValue)
        {
            throw new InvalidOperationException("Outbox is already processed");
        }

        ProcessedOnUtc = processedOnUtc;
    }

    public void SetAsProcessedWithError(DateTime processedOnUtc, string error)
    {
        SetAsProcessed(processedOnUtc);
        Error = error;
    }
}
