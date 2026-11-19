using Claims.Application.Repository;
using Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

public sealed class ClaimRepository(ClaimsContext context) : IClaimRepository
{
    public async Task<IReadOnlyList<Claim>> GetAllAsync() => await context.Claims.ToListAsync();

    public Task<Claim?> GetByIdAsync(string id) =>
        context.Claims.SingleOrDefaultAsync(claim => claim.Id == id);

    public Task<bool> HasClaimsForCoverAsync(string coverId) =>
        context.Claims.AnyAsync(claim => claim.CoverId == coverId);

    public async Task AddAsync(Claim claim)
    {
        context.Claims.Add(claim);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var claim = await GetByIdAsync(id);
        if (claim is null)
        {
            return;
        }

        context.Claims.Remove(claim);
        await context.SaveChangesAsync();
    }
}
