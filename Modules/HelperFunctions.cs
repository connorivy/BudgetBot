using BudgetBot.Database;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  public static class HelperFunctions
  {
    public static Transaction SelectedTransaction { get; set; }

    public async static Task<BudgetCategory> GetCategory(BudgetBotEntities _db, string cat, DateTimeOffset date)
    {
      var budget = await GetMonthlyBudget(_db, date);
      var category = budget.Budgets.Where(x => x.Name == cat).FirstOrDefault();

      return category;
    }

    private static MonthlyBudget _currentBudget;
    public async static Task<MonthlyBudget> GetMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date)
    {
      if (date.Month == _currentBudget?.Date.Month && date.Year == _currentBudget?.Date.Year)
        return _currentBudget;

      var budget = await _db.MonthlyBudgets
          .AsAsyncEnumerable()
          .Take(1)
          .Where(b => b.Date.Year == date.Year && b.Date.Month == date.Month)
          .FirstOrDefaultAsync();

      if (budget != null)
      {
        budget.Budgets = await _db.BudgetCategories
          .AsAsyncEnumerable()
          .Where(b => b.MonthlyBudget == budget)
          .ToListAsync();
      }
      else
      {
        budget = await CreateMonthlyBudget(_db, date);
      }
      _currentBudget = budget;

      return budget;
    }

    public async static Task<MonthlyBudget> CreateMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date)
    {
      MonthlyBudget monthlyBudget = null;
      var defaultTemplate = await _db.MonthlyBudgetTemplates
          .AsAsyncEnumerable()
          .Take(1)
          .Where(b => b.IsDefault == true)
          .FirstOrDefaultAsync();

      var budgetName = $"Budget for {date:MMMM} {date:yyyy}";
      var budgetsList = new List<BudgetCategory>();
      if (defaultTemplate != null)
      {
        budgetsList = defaultTemplate.Budgets;
        budgetName = defaultTemplate.Name.Replace("MMMM", date.ToString("MMMM")).Replace("yyyy", date.ToString("yyyy"));
      }
      else
      {
        //var lastMonth = date.AddMonths(-1);
        //if (lastMonth.Month == _currentBudget.Date.Month && lastMonth.Year == _currentBudget.Date.Year)
        //  budgetsList = _currentBudget.Budgets;
      }

      monthlyBudget = new MonthlyBudget
      {
        Date = GetEndOfMonth(date),
        Name = budgetName,
        Budgets = budgetsList
      };
      await _db.AddAsync(monthlyBudget);
      await _db.SaveChangesAsync();

      return monthlyBudget;
    }

    public static async Task<Transaction> GetTransaction(BudgetBotEntities _db, List<IEmbed> embeds)
    {
      if (embeds.Count != 1)
        return null;

      var titleParts = embeds[0].Title.Split(':');
      if (titleParts.Length == 2)
      {
        var id = long.Parse(titleParts[1].ToString().Trim());

        var transaction = await _db.Transactions
          .AsAsyncEnumerable()
          .Where(b => b.Id == id)
          .FirstOrDefaultAsync();
        return transaction;
      }

      return null;
    }

    public static DateTimeOffset GetEndOfMonth(DateTimeOffset date)
    {
      return new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Offset);
    }
  }
}
