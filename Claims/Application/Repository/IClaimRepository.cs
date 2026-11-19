using Claims.Domain;

namespace Claims.Application.Repository;

public interface IClaimRepository
{
    Task<IReadOnlyList<Claim>> GetAllAsync();
    Task<Claim?> GetByIdAsync(string id);
    Task<bool> HasClaimsForCoverAsync(string coverId);
    Task AddAsync(Claim claim);
    Task DeleteAsync(string id);
}
