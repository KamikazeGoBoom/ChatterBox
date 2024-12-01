using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ChatterBox.Models;

namespace ChatterBox.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private static readonly TimeZoneInfo _philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            // Increase command timeout for migrations
            Database.SetCommandTimeout(60);
        }

        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Contact> Contacts { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<GroupMember> GroupMembers { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        private DateTime GetPhilippinesTime()
        {
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, _philippinesTimeZone);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entityEntry in entries)
            {
                // Handle created timestamps
                if (entityEntry.State == EntityState.Added)
                {
                    switch (entityEntry.Entity)
                    {
                        case Message message:
                            if (message.SentAt == default)
                                message.SentAt = GetPhilippinesTime();
                            break;
                        case Notification notification:
                            if (notification.CreatedAt == default)
                                notification.CreatedAt = GetPhilippinesTime();
                            break;
                        case Contact contact:
                            if (contact.CreatedAt == default)
                                contact.CreatedAt = GetPhilippinesTime();
                            break;
                        case Group group:
                            if (group.CreatedAt == default)
                                group.CreatedAt = GetPhilippinesTime();
                            break;
                        case GroupMember groupMember:
                            if (groupMember.JoinedAt == default)
                                groupMember.JoinedAt = GetPhilippinesTime();
                            break;
                    }
                }

                // Handle LastSeen updates for ApplicationUser
                if (entityEntry.Entity is ApplicationUser user && entityEntry.State == EntityState.Modified)
                {
                    var lastSeenProperty = entityEntry.Property("LastSeen");
                    if (lastSeenProperty != null && lastSeenProperty.IsModified)
                    {
                        user.LastSeen = GetPhilippinesTime();
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Message configurations
            builder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Receiver)
                    .WithMany()
                    .HasForeignKey(m => m.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Group)
                    .WithMany()
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(m => m.SentAt)
                    .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

                // Add indexes for message querying
                entity.HasIndex(m => m.SentAt);
                entity.HasIndex(m => new { m.SenderId, m.ReceiverId });
                entity.HasIndex(m => m.GroupId);
            });

            // Contact configurations
            builder.Entity<Contact>(entity =>
            {
                entity.HasKey(c => c.Id);  // Using Id instead of ContactId

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ContactUser)
                    .WithMany()
                    .HasForeignKey(c => c.ContactUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(c => c.CreatedAt)
                    .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");
            });

            // Group configurations
            builder.Entity<Group>(entity =>
            {
                entity.HasOne(g => g.CreatedBy)
                    .WithMany()
                    .HasForeignKey(g => g.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(g => g.CreatedAt)
                    .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

                // Add index for group querying
                entity.HasIndex(g => g.CreatedAt);
            });

            // GroupMember configurations
            builder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(gm => new { gm.GroupId, gm.UserId });

                entity.HasOne(gm => gm.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(gm => gm.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gm => gm.User)
                    .WithMany()
                    .HasForeignKey(gm => gm.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(gm => gm.JoinedAt)
                    .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

                // Add index for group member querying
                entity.HasIndex(gm => gm.JoinedAt);
            });

            // Notification configurations
            builder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(n => n.CreatedAt)
                    .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

                // Add indexes for notification querying
                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.CreatedAt);
            });
        }
    }
}