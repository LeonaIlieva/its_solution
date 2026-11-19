using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Claims.Infrastructure.Persistence;

public sealed class ClaimsContext(DbContextOptions<ClaimsContext> options) : DbContext(options)
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Cover> Covers => Set<Cover>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var claims = modelBuilder.Entity<Claim>();
        claims.ToCollection("claims");
        claims.HasKey(claim => claim.Id);
        claims.Property(claim => claim.Id).HasElementName("_id");
        claims.Property(claim => claim.CoverId).HasElementName("coverId");
        claims.Property(claim => claim.Created).HasElementName("created");
        claims.Property(claim => claim.Name).HasElementName("name");
        claims.Property(claim => claim.Type).HasElementName("claimType");
        claims.Property(claim => claim.DamageCost).HasElementName("damageCost");

        var covers = modelBuilder.Entity<Cover>();
        covers.ToCollection("covers");
        covers.HasKey(cover => cover.Id);
        covers.Property(cover => cover.Id).HasElementName("_id");
        covers.Property(cover => cover.StartDate).HasElementName("startDate");
        covers.Property(cover => cover.EndDate).HasElementName("endDate");
        covers.Property(cover => cover.Type).HasElementName("claimType");
        covers.Property(cover => cover.Premium).HasElementName("premium");
    }
}
