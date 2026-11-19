using System.Collections.Concurrent;
using System.Threading.Channels;
using Claims.Application;
using Claims.Application.Repository;
using Claims.Application.Service;
using Claims.Auditing;
using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Claims.Tests;

public class AuditingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DateTimeOffset EventTime = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_ReturnsWhileAuditDatabaseWriteIsStillPending()
    {
        var queue = new AuditQueue(new FixedTimeProvider());
        var state = new PersistenceState();
        using var provider = CreateProvider(state);
        using var worker = CreateWorker(queue, provider);
        await worker.StartAsync(CancellationToken.None);
        var repository = new ClaimRepository();
        var service = new ClaimService(repository, queue);

        try
        {
            var claim = await service.CreateAsync(new Claim { Name = "Collision" }).WaitAsync(Timeout);
            await state.Entered.Task.WaitAsync(Timeout);

            Assert.Equal(claim.Id, repository.CreatedId);
            Assert.Empty(state.Saved);
        }
        finally
        {
            state.Release.TrySetResult();
            await worker.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        }

        var audit = Assert.IsType<ClaimAudit>(Assert.Single(state.Saved));
        Assert.Equal(repository.CreatedId, audit.ClaimId);
        Assert.Equal("POST", audit.HttpRequestType);
    }

    [Fact]
    public async Task Shutdown_DrainsBothEntityTypesAndOperationsInOrderUsingFreshScopes()
    {
        var queue = new AuditQueue(new FixedTimeProvider());
        await queue.AuditClaimAsync("claim-create", AuditOperation.Create);
        await queue.AuditClaimAsync("claim-delete", AuditOperation.Delete);
        await queue.AuditCoverAsync("cover-create", AuditOperation.Create);
        await queue.AuditCoverAsync("cover-delete", AuditOperation.Delete);
        var state = new PersistenceState();
        using var provider = CreateProvider(state);
        using var worker = CreateWorker(queue, provider);
        await worker.StartAsync(CancellationToken.None);
        await state.Entered.Task.WaitAsync(Timeout);

        var shutdown = worker.StopAsync(CancellationToken.None);
        try
        {
            Assert.False(shutdown.IsCompleted);
            Assert.Empty(state.Saved);
        }
        finally
        {
            state.Release.TrySetResult();
            await shutdown.WaitAsync(Timeout);
        }

        Assert.Collection(state.Saved,
            entry => AssertClaim(entry, "claim-create", "POST"),
            entry => AssertClaim(entry, "claim-delete", "DELETE"),
            entry => AssertCover(entry, "cover-create", "POST"),
            entry => AssertCover(entry, "cover-delete", "DELETE"));
        Assert.Equal(4, state.ContextIds.Distinct().Count());
        Assert.Equal(4, state.DisposedContexts);
        await Assert.ThrowsAsync<ChannelClosedException>(() => queue.AuditCoverAsync("late", AuditOperation.Create).AsTask());
    }

    [Fact]
    public async Task FullQueue_WaitsForCapacityWithoutDroppingEvents()
    {
        var queue = new AuditQueue(new FixedTimeProvider());
        for (var index = 0; index < 1000; index++)
            await queue.AuditClaimAsync(index.ToString(), AuditOperation.Create);

        var pending = queue.AuditCoverAsync("last", AuditOperation.Delete).AsTask();
        Assert.False(pending.IsCompleted);
        await using var reader = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal("0", reader.Current.EntityId);
        await pending.WaitAsync(Timeout);
        queue.Complete();

        var entries = new List<AuditEntry>();
        while (await reader.MoveNextAsync())
            entries.Add(reader.Current);

        Assert.Equal(1000, entries.Count);
        Assert.Equal(Enumerable.Range(1, 999).Select(index => index.ToString()),
            entries.Take(999).Select(entry => entry.EntityId));
        Assert.Equal(new AuditEntry("last", AuditTarget.Cover, AuditOperation.Delete, EventTime.LocalDateTime), entries[^1]);
    }

    [Fact]
    public async Task PersistenceFailure_FaultsWorkerInsteadOfSilentlyDroppingAudit()
    {
        var queue = new AuditQueue(new FixedTimeProvider());
        await queue.AuditClaimAsync("claim-id", AuditOperation.Create);
        var state = new PersistenceState { Failure = new DbUpdateException("Database unavailable") };
        state.Release.SetResult();
        using var provider = CreateProvider(state);
        using var worker = CreateWorker(queue, provider);
        await worker.StartAsync(CancellationToken.None);

        try
        {
            Assert.NotNull(worker.ExecuteTask);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => worker.ExecuteTask.WaitAsync(Timeout));
            Assert.Same(state.Failure, error);
            Assert.Empty(state.Saved);
        }
        finally
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => worker.StopAsync(CancellationToken.None));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_EnqueuesAuditEvenWhenEntityDoesNotExist(bool isCover)
    {
        var queue = new AuditQueue(new FixedTimeProvider());
        if (isCover)
            await new CoverService(new CoverRepository(), new ClaimRepository(), queue).TryDeleteAsync("missing");
        else
            await new ClaimService(new ClaimRepository(), queue).DeleteAsync("missing");
        queue.Complete();

        var entries = new List<AuditEntry>();
        await foreach (var entry in queue.ReadAllAsync(CancellationToken.None))
            entries.Add(entry);

        Assert.Equal(new AuditEntry("missing", isCover ? AuditTarget.Cover : AuditTarget.Claim,
            AuditOperation.Delete, EventTime.LocalDateTime), Assert.Single(entries));
    }

    private static void AssertClaim(object entry, string id, string requestType)
    {
        var audit = Assert.IsType<ClaimAudit>(entry);
        Assert.Equal(id, audit.ClaimId);
        Assert.Equal(requestType, audit.HttpRequestType);
        Assert.Equal(EventTime.LocalDateTime, audit.Created);
    }

    private static void AssertCover(object entry, string id, string requestType)
    {
        var audit = Assert.IsType<CoverAudit>(entry);
        Assert.Equal(id, audit.CoverId);
        Assert.Equal(requestType, audit.HttpRequestType);
        Assert.Equal(EventTime.LocalDateTime, audit.Created);
    }

    private static ServiceProvider CreateProvider(PersistenceState state) => new ServiceCollection()
        .AddScoped<AuditContext>(_ => new TestAuditContext(state))
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static Auditer CreateWorker(AuditQueue queue, ServiceProvider provider) =>
        new(queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<Auditer>.Instance);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => EventTime;
    }

    private sealed class PersistenceState
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<object> Saved { get; } = new();
        public ConcurrentQueue<Guid> ContextIds { get; } = new();
        public int DisposedContexts;
        public DbUpdateException? Failure { get; init; }
    }

    private sealed class TestAuditContext(PersistenceState state) : AuditContext(
        new DbContextOptionsBuilder<AuditContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true")
            .Options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            state.Entered.TrySetResult();
            await state.Release.Task.WaitAsync(cancellationToken);
            if (state.Failure is not null)
                throw state.Failure;

            state.ContextIds.Enqueue(ContextId.InstanceId);
            state.Saved.Enqueue(Assert.Single(ChangeTracker.Entries()).Entity);
            return 1;
        }

        public override async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref state.DisposedContexts);
            await base.DisposeAsync();
        }
    }

    private sealed class ClaimRepository : IClaimRepository
    {
        public string? CreatedId { get; private set; }
        public Task AddAsync(Claim claim)
        {
            CreatedId = claim.Id;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id) => Task.CompletedTask;
        public Task<IReadOnlyList<Claim>> GetAllAsync() => throw new NotSupportedException();
        public Task<Claim?> GetByIdAsync(string id) => throw new NotSupportedException();
        public Task<bool> HasClaimsForCoverAsync(string coverId) => Task.FromResult(false);
    }

    private sealed class CoverRepository : ICoverRepository
    {
        public Task DeleteAsync(string id) => Task.CompletedTask;
        public Task AddAsync(Cover cover) => throw new NotSupportedException();
        public Task<IReadOnlyList<Cover>> GetAllAsync() => throw new NotSupportedException();
        public Task<Cover?> GetByIdAsync(string id) => throw new NotSupportedException();
    }
}
