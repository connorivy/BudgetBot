using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetBot.Database
{
  public class BudgetBotEntities: DbContext
  {
    public virtual DbSet<Transaction> Transactions { get; set; }
    public virtual DbSet<Transfer> Transfers { get; set; }
    public virtual DbSet<MonthlyBudget> MonthlyBudgets { get; set; }
    public virtual DbSet<MonthlyBudgetTemplate> MonthlyBudgetTemplates { get; set; }
    public virtual DbSet<Bucket> Buckets { get; set; }
    public virtual DbSet<BudgetCategory> BudgetCategories { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = "BudgetBot.db" };
      var connectionString = connectionStringBuilder.ToString();
      var connection = new SqliteConnection(connectionString);
      optionsBuilder.UseSqlite(connection);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<Transaction>()
        .HasOne(e => e.BudgetCategory)
        .WithMany(e => e.Transactions);

      modelBuilder.Entity<Transaction>()
        .HasOne(e => e.Bucket)
        .WithMany(e => e.Transactions);
    }
  }
}
