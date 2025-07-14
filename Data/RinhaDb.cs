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
        
        base.OnModelCreating(modelBuilder);
    }
    
    public DbSet<Payment> Payments { get; set; }
}