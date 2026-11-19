using Claims.Application.Repository;
using Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

public sealed class CoverRepository(ClaimsContext context) : ICoverRepository
{
    public async Task<IReadOnlyList<Cover>> GetAllAsync() => await context.Covers.ToListAsync();

    public Task<Cover?> GetByIdAsync(string id) =>
        context.Covers.SingleOrDefaultAsync(cover => cover.Id == id);

    public async Task AddAsync(Cover cover)
    {
        context.Covers.Add(cover);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var cover = await GetByIdAsync(id);
        if (cover is null)
        {
            return;
        }

        context.Covers.Remove(cover);
        await context.SaveChangesAsync();
    }
}
