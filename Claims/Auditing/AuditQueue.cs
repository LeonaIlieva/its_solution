using System.Threading.Channels;
using Claims.Application;

namespace Claims.Auditing;

public sealed class AuditQueue(TimeProvider timeProvider) : IAuditWriter
{
    private readonly Channel<AuditEntry> entries = Channel.CreateBounded<AuditEntry>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        AllowSynchronousContinuations = false
    });

    public ValueTask AuditClaimAsync(string id, AuditOperation operation) =>
        EnqueueAsync(id, AuditTarget.Claim, operation);

    public ValueTask AuditCoverAsync(string id, AuditOperation operation) =>
        EnqueueAsync(id, AuditTarget.Cover, operation);

    public IAsyncEnumerable<AuditEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        entries.Reader.ReadAllAsync(cancellationToken);

    public void Complete() => entries.Writer.TryComplete();

    private ValueTask EnqueueAsync(string id, AuditTarget target, AuditOperation operation)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation), operation, null);

        return entries.Writer.WriteAsync(new AuditEntry(id, target, operation, timeProvider.GetLocalNow().DateTime));
    }
}

public sealed record AuditEntry(string EntityId, AuditTarget Target, AuditOperation Operation, DateTime Created);

public enum AuditTarget
{
    Claim,
    Cover
}
