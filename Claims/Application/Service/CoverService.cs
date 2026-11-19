using Claims.Application.Repository;
using Claims.Domain;

namespace Claims.Application.Service;

public sealed class CoverService(ICoverRepository covers, IClaimRepository claims, IAuditWriter auditWriter)
{
    public Task<IReadOnlyList<Cover>> GetAllAsync() => covers.GetAllAsync();

    public Task<Cover?> GetByIdAsync(string id) => covers.GetByIdAsync(id);

    public decimal ComputePremium(DateTime startDate, DateTime endDate, CoverType coverType) =>
        PremiumCalculator.Calculate(startDate, endDate, coverType);

    public async Task<Cover> CreateAsync(Cover cover)
    {
        cover.Id = Guid.NewGuid().ToString();
        cover.Premium = ComputePremium(cover.StartDate, cover.EndDate, cover.Type);
        await covers.AddAsync(cover);
        await auditWriter.AuditCoverAsync(cover.Id, AuditOperation.Create);
        return cover;
    }

    public async Task<bool> TryDeleteAsync(string id)
    {
        await auditWriter.AuditCoverAsync(id, AuditOperation.Delete);
        if (await claims.HasClaimsForCoverAsync(id))
            return false;

        await covers.DeleteAsync(id);
        return true;
    }
}
