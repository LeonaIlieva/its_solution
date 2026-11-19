using Claims.Application;
using Claims.Application.Repository;
using Claims.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Tests.UnitTests;

internal sealed class ControllerTestDependencies : IClaimRepository, ICoverRepository, IAuditWriter, IDisposable
{
    private readonly ServiceProvider services = CreateServices();

    public List<Claim> Claims { get; } = [];
    public List<Cover> Covers { get; } = [];
    public List<(string EntityId, AuditOperation Operation)> ClaimAudits { get; } = [];
    public List<(string EntityId, AuditOperation Operation)> CoverAudits { get; } = [];

    public ControllerContext CreateControllerContext() => new()
    {
        HttpContext = new DefaultHttpContext { RequestServices = services }
    };

    Task<IReadOnlyList<Claim>> IClaimRepository.GetAllAsync() => Task.FromResult<IReadOnlyList<Claim>>(Claims);
    Task<IReadOnlyList<Cover>> ICoverRepository.GetAllAsync() => Task.FromResult<IReadOnlyList<Cover>>(Covers);
    Task<Claim?> IClaimRepository.GetByIdAsync(string id) => Task.FromResult(Claims.Find(claim => claim.Id == id));
    Task<Cover?> ICoverRepository.GetByIdAsync(string id) => Task.FromResult(Covers.Find(cover => cover.Id == id));
    Task<bool> IClaimRepository.HasClaimsForCoverAsync(string coverId) =>
        Task.FromResult(Claims.Exists(claim => claim.CoverId == coverId));

    public Task AddAsync(Claim claim)
    {
        Claims.Add(claim);
        return Task.CompletedTask;
    }

    public Task AddAsync(Cover cover)
    {
        Covers.Add(cover);
        return Task.CompletedTask;
    }

    Task IClaimRepository.DeleteAsync(string id)
    {
        Claims.RemoveAll(claim => claim.Id == id);
        return Task.CompletedTask;
    }

    Task ICoverRepository.DeleteAsync(string id)
    {
        Covers.RemoveAll(cover => cover.Id == id);
        return Task.CompletedTask;
    }

    public ValueTask AuditClaimAsync(string id, AuditOperation operation)
    {
        ClaimAudits.Add((id, operation));
        return ValueTask.CompletedTask;
    }

    public ValueTask AuditCoverAsync(string id, AuditOperation operation)
    {
        CoverAudits.Add((id, operation));
        return ValueTask.CompletedTask;
    }

    public void Dispose() => services.Dispose();

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }
}
