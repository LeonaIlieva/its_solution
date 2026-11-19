using Claims.Application.Repository;
using Claims.Domain;

namespace Claims.Application.Service;

public sealed class ClaimService(IClaimRepository claims, IAuditWriter auditWriter)
{
    public Task<IReadOnlyList<Claim>> GetAllAsync() => claims.GetAllAsync();

    public Task<Claim?> GetByIdAsync(string id) => claims.GetByIdAsync(id);

    public async Task<Claim> CreateAsync(Claim claim)
    {
        claim.Id = Guid.NewGuid().ToString();
        await claims.AddAsync(claim);
        await auditWriter.AuditClaimAsync(claim.Id, AuditOperation.Create);
        return claim;
    }

    public async Task DeleteAsync(string id)
    {
        await auditWriter.AuditClaimAsync(id, AuditOperation.Delete);
        await claims.DeleteAsync(id);
    }
}
