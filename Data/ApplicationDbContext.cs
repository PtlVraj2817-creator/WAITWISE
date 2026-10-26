using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WaitWise.Models;

namespace WaitWise.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Counter> Counters { get; set; } = null!;
        public DbSet<QueueTicket> QueueTickets { get; set; } = null!;
        public DbSet<ServiceLog> ServiceLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Counter -> Department
            builder.Entity<Counter>()
                .HasOne(c => c.Department)
                .WithMany(d => d.Counters)
                .HasForeignKey(c => c.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Counter -> CurrentTicket (active ticket at desk)
            builder.Entity<Counter>()
                .HasOne(c => c.CurrentTicket)
                .WithMany()
                .HasForeignKey(c => c.CurrentTicketId)
                .OnDelete(DeleteBehavior.Restrict);

            // QueueTicket -> Counter
            builder.Entity<QueueTicket>()
                .HasOne(q => q.Counter)
                .WithMany(c => c.TicketsServed)
                .HasForeignKey(q => q.CounterId)
                .OnDelete(DeleteBehavior.Restrict);

            // QueueTicket -> Department
            builder.Entity<QueueTicket>()
                .HasOne(q => q.Department)
                .WithMany(d => d.Tickets)
                .HasForeignKey(q => q.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ServiceLog -> Department
            builder.Entity<ServiceLog>()
                .HasOne(s => s.Department)
                .WithMany(d => d.ServiceLogs)
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ServiceLog -> Counter
            builder.Entity<ServiceLog>()
                .HasOne(s => s.Counter)
                .WithMany(c => c.ServiceLogs)
                .HasForeignKey(s => s.CounterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Performance Indexes
            builder.Entity<QueueTicket>()
                .HasIndex(q => q.Status);

            builder.Entity<QueueTicket>()
                .HasIndex(q => new { q.DepartmentId, q.Status });

            builder.Entity<QueueTicket>()
                .HasIndex(q => q.CreatedAt);

            builder.Entity<QueueTicket>()
                .HasIndex(q => q.TicketNumber);
        }
    }
}
