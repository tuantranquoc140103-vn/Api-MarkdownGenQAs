using MarkdownGenQAs.Models.DB;
using Microsoft.EntityFrameworkCore;

// namespace MarkdownGenQAs.Application;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
    {
    }

    public DbSet<OCRFile> OCRFiles { get; set; }
    public DbSet<CategoryFile> CategoryFiles { get; set; }
    public DbSet<LogMessage> LogMessages { get; set; }
    public DbSet<OCRFileJob> OCRFileJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CategoryFile entity
        modelBuilder.Entity<CategoryFile>(entity =>
        {
            entity.ToTable("CategoryFiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();

            // CategoryFile -> OCRFiles (One-to-Many)
            entity.HasMany(c => c.OCRFiles)
                  .WithOne(o => o.CategoryFile)
                  .HasForeignKey(o => o.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure OCRFile entity
        modelBuilder.Entity<OCRFile>(entity =>
        {
            entity.ToTable("OCRFiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ObjectKeyFilePdf).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().IsRequired();

            // Note: Inverse relationships are configured in dependent entities or above
        });

        // Configure LogMessage entity
        modelBuilder.Entity<LogMessage>(entity =>
        {
            entity.ToTable("LogMessages");
            entity.HasKey(e => e.Id);

            // OCRFile -> LogMessage (One-to-One)
            entity.HasOne(l => l.OCRFile)
                  .WithOne(o => o.LogMessage)
                  .HasForeignKey<LogMessage>(l => l.OCRFileId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OCRFileJob entity (One-to-One with OCRFile)
        modelBuilder.Entity<OCRFileJob>(entity =>
        {
            entity.ToTable("OCRFileJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileJobId).HasMaxLength(255);
            entity.Property(e => e.WorkerJobId).HasMaxLength(255);

            entity.HasOne(j => j.OCRFile)
                  .WithOne(o => o.OCRFileJob)
                  .HasForeignKey<OCRFileJob>(j => j.OCRFileId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BaseEntity properties for all entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var entity = modelBuilder.Entity(entityType.ClrType);

                entity.Property<Guid>("Id")
                    .ValueGeneratedOnAdd();

                entity.Property<DateTime>("CreatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP"); // Standard SQL for Npgsql

                entity.Property<DateTime>("UpdatedAt")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            var entity = (BaseEntity)entityEntry.Entity;

            if (entityEntry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
