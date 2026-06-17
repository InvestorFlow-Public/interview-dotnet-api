using Microsoft.EntityFrameworkCore;
using interview_dotnet_api.Models;

namespace interview_dotnet_api.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<NotificationItem> Notifications { get; set; } = null!;
    }
}
