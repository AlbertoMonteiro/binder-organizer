using BinderPokedex.Domain;
using Microsoft.EntityFrameworkCore;

namespace BinderPokedex.Persistence;

public sealed class CollectedSlot
{
    public int PokemonId { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
    public CardRarity Rarity { get; set; }
}

public sealed class BinderDbContext(DbContextOptions<BinderDbContext> options) : DbContext(options)
{
    public DbSet<CollectedSlot> Collected => Set<CollectedSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectedSlot>(e =>
        {
            e.ToTable("collected");
            e.HasKey(x => x.PokemonId);
            e.Property(x => x.PokemonId).ValueGeneratedNever();        });
    }
}
