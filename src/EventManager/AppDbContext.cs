using Microsoft.EntityFrameworkCore;

namespace EventManager;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Match> Matches => Set<Match>();
}
