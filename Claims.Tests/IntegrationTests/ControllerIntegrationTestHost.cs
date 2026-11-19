using Claims.Application;
using Claims.Application.Repository;
using Claims.Application.Service;
using Claims.Application.Validation;
using Claims.Controllers;
using Claims.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Tests.IntegrationTests;

internal static class ControllerIntegrationTestHost
{
    internal static readonly DateTime Today = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static TestServer CreateServer(TestStore store) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton<IClaimRepository>(store);
            services.AddSingleton<ICoverRepository>(store);
            services.AddSingleton<IAuditWriter>(store);
            services.AddScoped<ClaimService>();
            services.AddScoped<CoverService>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            services.AddScoped<IValidator<Claim>, ClaimValidator>();
            services.AddScoped<IValidator<Cover>, CoverValidator>();
            services.AddControllers().AddApplicationPart(typeof(ClaimsController).Assembly);
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Today.AddHours(12));
    }

    internal sealed class TestStore : IClaimRepository, ICoverRepository, IAuditWriter
    {
        public int ClaimWrites { get; private set; }
        public int CoverWrites { get; private set; }
        public int AuditWrites { get; private set; }

        Task<Cover?> ICoverRepository.GetByIdAsync(string id) => Task.FromResult<Cover?>(id == "cover-id"
            ? new Cover { Id = id, StartDate = Today, EndDate = Today.AddDays(30) }
            : null);

        public Task AddAsync(Claim claim)
        {
            ClaimWrites++;
            return Task.CompletedTask;
        }

        public Task AddAsync(Cover cover)
        {
            CoverWrites++;
            return Task.CompletedTask;
        }

        public ValueTask AuditClaimAsync(string id, AuditOperation operation)
        {
            AuditWrites++;
            return ValueTask.CompletedTask;
        }

        public ValueTask AuditCoverAsync(string id, AuditOperation operation)
        {
            AuditWrites++;
            return ValueTask.CompletedTask;
        }

        Task<IReadOnlyList<Claim>> IClaimRepository.GetAllAsync() => throw new NotSupportedException();
        Task<IReadOnlyList<Cover>> ICoverRepository.GetAllAsync() => throw new NotSupportedException();
        Task<Claim?> IClaimRepository.GetByIdAsync(string id) => throw new NotSupportedException();
        Task<bool> IClaimRepository.HasClaimsForCoverAsync(string coverId) => throw new NotSupportedException();
        public Task DeleteAsync(string id) => throw new NotSupportedException();
    }
}
