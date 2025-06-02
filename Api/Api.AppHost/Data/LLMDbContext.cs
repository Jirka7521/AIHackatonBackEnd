using Microsoft.EntityFrameworkCore;

namespace LLM.Data
{
    public class LLMDbContext : DbContext
    {
        public LLMDbContext(DbContextOptions<LLMDbContext> options) : base(options)
        {
        }

        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Attachment> Attachments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Workspace configuration
            modelBuilder.Entity<Workspace>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            // Chat configuration
            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
                
                entity.HasOne(e => e.Workspace)
                    .WithMany(e => e.Chats)
                    .HasForeignKey(e => e.WorkspaceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            });

            // Message configuration
            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Role).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                
                entity.HasOne(e => e.Chat)
                    .WithMany(e => e.Messages)
                    .HasForeignKey(e => e.ChatId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Attachment configuration
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                
                entity.HasOne(e => e.Workspace)
                    .WithMany(e => e.Attachments)
                    .HasForeignKey(e => e.WorkspaceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is Workspace || e.Entity is Chat)
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entityEntry in entries)
            {
                if (entityEntry.Entity is Workspace workspace)
                {
                    if (entityEntry.State == EntityState.Added)
                    {
                        workspace.CreatedAt = DateTime.UtcNow;
                    }
                    workspace.UpdatedAt = DateTime.UtcNow;
                }
                else if (entityEntry.Entity is Chat chat)
                {
                    if (entityEntry.State == EntityState.Added)
                    {
                        chat.CreatedAt = DateTime.UtcNow;
                    }
                    chat.UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
} 