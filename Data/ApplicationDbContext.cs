using Microsoft.EntityFrameworkCore;
using SoulScript.Model;
using System.Diagnostics;

namespace SoulScript.Data;

public partial class ApplicationDbContext : DbContext

{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntryTag> EntryTags => Set<EntryTag>();
    public DbSet<Mood> Moods => Set<Mood>();
    public DbSet<EntryMood> EntryMoods => Set<EntryMood>();
    public DbSet<Streak> Streaks => Set<Streak>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "coursework.db");
            Debug.WriteLine($"DB PATH: {dbPath}");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JournalEntry: one per day
        modelBuilder.Entity<JournalEntry>()
            .HasIndex(e => e.EntryDate)
            .IsUnique();


        // JournalEntry -> Category (optional)
        modelBuilder.Entity<JournalEntry>()
            .HasOne(e => e.Category)
            .WithMany(c => c.Entries)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // EntryTag (JournalEntry <-> Tag)
        modelBuilder.Entity<EntryTag>()
            .HasOne(et => et.JournalEntry)
            .WithMany(e => e.EntryTags)
            .HasForeignKey(et => et.JournalEntryId);

        modelBuilder.Entity<EntryTag>()
            .HasOne(et => et.Tag)
            .WithMany(t => t.EntryTags)
            .HasForeignKey(et => et.TagId);

        // EntryMood (JournalEntry <-> Mood)
        modelBuilder.Entity<EntryMood>()
            .HasOne(em => em.JournalEntry)
            .WithMany(e => e.EntryMoods)
            .HasForeignKey(em => em.JournalEntryId);

        modelBuilder.Entity<EntryMood>()
            .HasOne(em => em.Mood)
            .WithMany(m => m.EntryMoods)
            .HasForeignKey(em => em.MoodId);
    }
}
