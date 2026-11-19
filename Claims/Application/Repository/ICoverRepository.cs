using Claims.Domain;

namespace Claims.Application.Repository;

public interface ICoverRepository
{
    Task<IReadOnlyList<Cover>> GetAllAsync();
    Task<Cover?> GetByIdAsync(string id);
    Task AddAsync(Cover cover);
    Task DeleteAsync(string id);
}
