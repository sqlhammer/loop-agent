using Microsoft.EntityFrameworkCore;

namespace EventManager;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
