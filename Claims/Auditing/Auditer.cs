using Claims.Application;
using Microsoft.EntityFrameworkCore;

namespace Claims.Auditing;

public sealed class Auditer(
    AuditQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<Auditer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in queue.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AuditContext>();
            var requestType = entry.Operation switch
            {
                AuditOperation.Create => "POST",
                AuditOperation.Delete => "DELETE",
                _ => throw new ArgumentOutOfRangeException(nameof(entry.Operation), entry.Operation, null)
            };

            switch (entry.Target)
            {
                case AuditTarget.Claim:
                    context.ClaimAudits.Add(new ClaimAudit
                    {
                        ClaimId = entry.EntityId,
                        Created = entry.Created,
                        HttpRequestType = requestType
                    });
                    break;
                case AuditTarget.Cover:
                    context.CoverAudits.Add(new CoverAudit
                    {
                        CoverId = entry.EntityId,
                        Created = entry.Created,
                        HttpRequestType = requestType
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Target), entry.Target, null);
            }

            try
            {
                await context.SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateException exception)
            {
                logger.LogCritical(exception, "Failed to persist {Target} audit for {EntityId}. Stopping the audit worker.",
                    entry.Target, entry.EntityId);
                throw;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        try
        {
            if (ExecuteTask is not null)
                await ExecuteTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Audit queue could not be drained before the shutdown deadline; pending audits may be lost.");
            throw;
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }
}
