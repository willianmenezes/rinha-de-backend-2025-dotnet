using Microsoft.EntityFrameworkCore;

namespace RinhaBackend.Data;

public sealed class RinhaDb : DbContext
{
    public RinhaDb(DbContextOptions<RinhaDb> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");

            entity.HasKey(e => e.CorrelationId);

            entity.Property(e => e.CorrelationId)
                .HasColumnName("correlationid")
                .HasColumnType("uuid")
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.RequestedAt)
                .HasColumnName("requested_at")
                .IsRequired();

            entity.Property(e => e.Fallback)
                .HasColumnName("fallback")
                .IsRequired();
        });
        
        modelBuilder.Entity<HealthCheck>(entity =>
        {
            entity.ToTable("healthcheck");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .IsRequired();

            entity.Property(e => e.BestClient)
                .HasColumnName("best_client")
                .HasColumnType("char(8)")
                .IsRequired();

            entity.Property(e => e.RequestedAt)
                .HasColumnName("requested_at")
                .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<Payment> Payments { get; set; }
    public DbSet<HealthCheck> HealthCheck { get; set; }
}