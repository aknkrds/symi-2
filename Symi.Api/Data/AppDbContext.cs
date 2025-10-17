using Microsoft.EntityFrameworkCore;
using Symi.Api.Models;

namespace Symi.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrganizerKycApplication> OrganizerKycs => Set<OrganizerKycApplication>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<OrganizerContract> OrganizerContracts => Set<OrganizerContract>();
    public DbSet<OrganizerContractAcceptance> OrganizerContractAcceptances => Set<OrganizerContractAcceptance>();
    // New domain
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<MediaJob> MediaJobs => Set<MediaJob>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<PayoutPlan> PayoutPlans => Set<PayoutPlan>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
        });
    
        // Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
        });
    
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });
            entity.HasOne(ur => ur.User)
                  .WithMany()
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ur => ur.Role)
                  .WithMany()
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.TokenHash).IsUnique();
            entity.HasOne(r => r.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizerKycApplication>(entity =>
        {
            entity.HasIndex(k => k.UserId).IsUnique(); // a user has one active application
            entity.Property(k => k.Status).HasDefaultValue("pending");
        });

        modelBuilder.Entity<KycDocument>(entity =>
        {
            entity.HasOne(d => d.Application)
                  .WithMany(a => a.Documents)
                  .HasForeignKey(d => d.ApplicationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizerContract>(entity =>
        {
            entity.HasIndex(c => c.IsCurrent);
            entity.HasIndex(c => c.Version).IsUnique();
        });

        modelBuilder.Entity<OrganizerContractAcceptance>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.ContractId }).IsUnique();
        });

        // Events domain indexes
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PublishedAt);
        });

        modelBuilder.Entity<EventSession>(entity =>
        {
            entity.HasIndex(s => new { s.EventId, s.StartAt });
            entity.HasOne(s => s.Event)
                  .WithMany(e => e.Sessions)
                  .HasForeignKey(s => s.EventId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasIndex(t => new { t.EventId, t.Name });
            entity.HasOne(t => t.Event)
                  .WithMany(e => e.TicketTypes)
                  .HasForeignKey(t => t.EventId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaJob>(entity =>
        {
            entity.HasIndex(m => m.Status);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => new { o.UserId, o.Status });
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(oi => new { oi.OrderId, oi.TicketTypeId });
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(p => p.IdempotencyKey).IsUnique();
            entity.HasIndex(p => new { p.OrderId, p.Status });
        });
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasIndex(w => w.IdempotencyKey).IsUnique();
        });
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(t => t.QrToken).IsUnique();
            entity.HasIndex(t => new { t.EventId, t.Status });
        });
        modelBuilder.Entity<PayoutPlan>(entity =>
        {
            entity.HasIndex(p => new { p.EventId, p.Status });
            entity.HasIndex(p => p.ScheduledAt);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(p => p.CreatedAt);
            entity.HasIndex(p => p.OrganizerUserId);
            entity.HasIndex(p => p.EventId);
            entity.HasIndex(p => p.Status);
        });
        modelBuilder.Entity<PostReaction>(entity =>
        {
            entity.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();
        });
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasIndex(c => new { c.PostId, c.CreatedAt });
        });
        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasIndex(f => new { f.UserId, f.OrganizerUserId }).IsUnique();
        });
        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(r => new { r.TargetType, r.TargetId });
        });

        base.OnModelCreating(modelBuilder);
    }
}