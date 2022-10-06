using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BudgetBot.Database
{
  public class BudgetBotEntities: DbContext
  {
    public virtual DbSet<Transaction> Transactions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = "BudgetBot.db" };
      var connectionString = connectionStringBuilder.ToString();
      var connection = new SqliteConnection(connectionString);
      optionsBuilder.UseSqlite(connection);
    }
  }
}
