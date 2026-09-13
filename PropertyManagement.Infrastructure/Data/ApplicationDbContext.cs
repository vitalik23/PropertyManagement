using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<Residence> Residences => Set<Residence>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationApplicant> ApplicationApplicants => Set<ApplicationApplicant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Unit>()
            .Property(u => u.MonthlyRent)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Property>()
            .Property(p => p.IsRemoved)
            .HasDefaultValue(false);

        builder.Entity<Unit>()
            .Property(u => u.IsRemoved)
            .HasDefaultValue(false);

        // Property/Unit are soft-deleted (IsRemoved flag) — the app itself never issues a
        // physical DELETE on them. Every FK in the schema is Cascade except the three below,
        // which SQL Server refuses to create as Cascade: they'd form a second cascade path to
        // a table already reachable another way ("multiple cascade paths"/cycle), which SQL
        // Server rejects outright when the constraint is created. Each stays Restrict, and the
        // row still gets cleaned up via the other (Cascade) path when a delete does cascade
        // through it — Restrict here only blocks a delete that isn't already being cascaded in
        // from elsewhere.
        builder.Entity<Unit>()
            .HasOne(u => u.Property)
            .WithMany(p => p.Units)
            .HasForeignKey(u => u.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Unit>()
            .HasOne(u => u.UnitType)
            .WithMany()
            .HasForeignKey(u => u.UnitTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentalApplication>()
            .HasOne(a => a.Unit)
            .WithMany(u => u.RentalApplications)
            .HasForeignKey(a => a.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentalApplication>()
            .HasOne(a => a.ApplicantUser)
            .WithMany()
            .HasForeignKey(a => a.ApplicantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Lease>()
            .HasOne(l => l.Unit)
            .WithMany(u => u.Leases)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict); // 2nd path to Leases — see comment above (1st: Unit -> RentalApplication -> Lease)

        builder.Entity<Lease>()
            .HasOne(l => l.RentalApplication)
            .WithOne(a => a.Lease)
            .HasForeignKey<Lease>(l => l.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationStatusHistory>()
            .HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict); // 2nd path to ApplicationStatusHistories — see comment above (1st: User -> RentalApplication -> ApplicationStatusHistory)

        builder.Entity<ApplicationApplicant>()
            .HasOne(a => a.RentalApplication)
            .WithMany(r => r.CoApplicants)
            .HasForeignKey(a => a.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationApplicant>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict); // 2nd path to RentalApplications — see comment above (1st: User -> RentalApplication directly)

        builder.Entity<ApplicationApplicant>()
            .HasIndex(a => new { a.RentalApplicationId, a.UserId })
            .IsUnique();

        builder.Entity<Residence>()
            .HasOne(r => r.RentalApplication)
            .WithMany(a => a.Residences)
            .HasForeignKey(r => r.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IBaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
