using Email.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Email.Infrastructure.Persistense
{
    public class EmailDbContext : DbContext
    {
        public const string SchemaName = "email";

        public EmailDbContext(DbContextOptions<EmailDbContext> options) : base(options)
        {
        }

        public DbSet<EmailRequestHeader> EmailRequestHeaders { get; set; }
        public DbSet<EmailRequestDetail> EmailRequestDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(SchemaName);

            modelBuilder.Entity<EmailRequestHeader>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.To).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(512);
                entity.Property(e => e.ScheduleDateTime);
                entity.Property(e => e.CreatedAtUtc);

                entity.HasOne(e => e.Detail)
                      .WithOne(d => d!.Header)
                      .HasForeignKey<EmailRequestDetail>(d => d.EmailRequestHeaderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EmailRequestDetail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Body).IsRequired();
            });
        }
    }
}
