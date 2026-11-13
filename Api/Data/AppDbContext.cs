using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ShoppingItem> ShoppingItems => Set<ShoppingItem>();
    public DbSet<ProductCache> ProductCache => Set<ProductCache>();

    /// <summary>
    /// Own schema instead of "public": Supabase exposes "public" through its Data API,
    /// so keeping our tables (incl. password hashes) out of it means they're only reachable via this API.
    /// </summary>
    public const string Schema = "app";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Email).IsUnique();
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<Household>(h =>
        {
            h.Property(x => x.Name).HasMaxLength(100);
            h.Property(x => x.InviteCode).HasMaxLength(16);
            h.HasIndex(x => x.InviteCode).IsUnique();
            h.Property(x => x.MonthlyBudget).HasPrecision(12, 2);
        });

        modelBuilder.Entity<HouseholdMember>(m =>
        {
            m.HasKey(x => new { x.HouseholdId, x.UserId });
            m.HasOne(x => x.Household).WithMany(h => h.Members).HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            m.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            m.HasIndex(x => x.UserId); // "which households am I in?"
            m.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Item>(i =>
        {
            i.Property(x => x.Name).HasMaxLength(200);
            i.Property(x => x.Barcode).HasMaxLength(20);
            i.Property(x => x.ImageUrl).HasMaxLength(500);
            i.Property(x => x.Price).HasPrecision(12, 2);
            i.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            i.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            i.HasOne(x => x.AddedBy).WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
            // Inventory list: a household's items in the fridge, soonest expiry first
            i.HasIndex(x => new { x.HouseholdId, x.Status, x.ExpiryDate });
        });

        modelBuilder.Entity<ShoppingItem>(s =>
        {
            s.Property(x => x.Name).HasMaxLength(200);
            s.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            s.HasIndex(x => x.HouseholdId);
        });

        modelBuilder.Entity<ProductCache>(p =>
        {
            p.HasKey(x => x.Barcode);
            p.Property(x => x.Barcode).HasMaxLength(20);
            p.Property(x => x.Name).HasMaxLength(200);
            p.Property(x => x.Brand).HasMaxLength(200);
            p.Property(x => x.ImageUrl).HasMaxLength(500);
        });
    }
}
