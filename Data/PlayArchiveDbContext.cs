using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using PlayArchive.Models;

namespace PlayArchive.Data
{
    public class PlayArchiveDbContext : DbContext
    {
        public DbSet<GameItem> Games { get; set; } = null!;
        public DbSet<GameSession> Sessions { get; set; } = null!;

        public PlayArchiveDbContext() { }

        public PlayArchiveDbContext(DbContextOptions<PlayArchiveDbContext> options)
            : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayArchive");
                Directory.CreateDirectory(folder);
                string dbPath = Path.Combine(folder, "PlayArchive.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GameItem>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Title).IsRequired().HasMaxLength(100);
                entity.Property(g => g.ExecutableName).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasOne(s => s.GameItem)
                      .WithMany(g => g.Sessions)
                      .HasForeignKey(s => s.GameItemId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
