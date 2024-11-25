using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ChatterBox.Models;

namespace ChatterBox.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Contact> Contacts { get; set; } = null!;
        public DbSet<Group> Groups { get; set; } = null!;
        public DbSet<GroupMember> GroupMembers { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Message configurations
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Group)
                .WithMany()
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contact configurations
            builder.Entity<Contact>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Contact>()
                .HasOne(c => c.ContactUser)
                .WithMany()
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Group configurations
            builder.Entity<Group>()
                .HasOne(g => g.CreatedBy)
                .WithMany()
                .HasForeignKey(g => g.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // GroupMember configurations
            builder.Entity<GroupMember>()
                .HasKey(gm => new { gm.GroupId, gm.UserId });

            builder.Entity<GroupMember>()
                .HasOne(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GroupMember>()
                .HasOne(gm => gm.User)
                .WithMany()
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification configurations
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure default values for timestamps
            builder.Entity<Message>()
                .Property(m => m.SentAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Entity<Notification>()
                .Property(n => n.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Entity<Contact>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Entity<Group>()
                .Property(g => g.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Entity<GroupMember>()
                .Property(gm => gm.JoinedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}