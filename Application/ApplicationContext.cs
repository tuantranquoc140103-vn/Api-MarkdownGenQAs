using MarkdownGenQAs.Models.DB;
using Microsoft.EntityFrameworkCore;

// namespace MarkdownGenQAs.Application;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
    {
    }

    public DbSet<FileMetadata> FileMetadatas { get; set; }
    public DbSet<CategoryFile> CategoryFiles { get; set; }
    public DbSet<LogMessage> LogMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure FileMetadata entity
        modelBuilder.Entity<FileMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FileType).IsRequired();
            entity.Property(e => e.ObjectKeyMarkdownOcr).IsRequired();
            
            // Configure relationship with CategoryFile
            entity.HasOne(e => e.CategoryFile)
                  .WithMany(c => c.FileMetadatas)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
            
            // Configure relationship with LogMessage
            entity.HasOne(e => e.LogMessage)
                  .WithOne(l => l.FileMetadata)
                  .HasForeignKey<LogMessage>(l => l.FileMetadataId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.Status).HasConversion<string>().IsRequired();
        });

        // Configure CategoryFile entity
        modelBuilder.Entity<CategoryFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure LogMessage entity
        modelBuilder.Entity<LogMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired();
        });

        // Configure BaseEntity properties for all entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<Guid>("Id")
                    .ValueGeneratedOnAdd();
                
                modelBuilder.Entity(entityType.ClrType)
                    .Property<DateTime>("CreatedAt")
                    .HasDefaultValueSql("NOW()");
                
                modelBuilder.Entity(entityType.ClrType)
                    .Property<DateTime>("UpdatedAt")
                    .HasDefaultValueSql("NOW()");
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
