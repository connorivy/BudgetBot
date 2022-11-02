using BudgetBot.Database;
using Discord;
using Discord.Interactions;
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
  [Group("budget", "Group description")]
  public class BudgetCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;

    public BudgetCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    #region message commands

    #endregion

    [SlashCommand("create", "creates a new budget")]
    public async Task CreateCommand(string name, decimal limit, bool isIncome = false)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var sb = new StringBuilder();

      name = name.ToLower();
      limit = Math.Abs(limit);

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now, Context.Guild);
      monthlyBudget.Budgets ??= new List<BudgetCategory>();

      if (monthlyBudget.Budgets.Any(x => x.Name == name))
      {
        sb.AppendLine($"There is already a budget named {name} for {monthlyBudget.Date:Y}");
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = sb.ToString();
        });
        return;
      }

      var budget = new BudgetCategory
      {
        Balance = 0,
        StartingAmount = 0,
        Name = name,
        TargetAmount = isIncome ? limit : limit * -1,
        MonthlyBudget = monthlyBudget,
      };

      monthlyBudget.Budgets.Add(budget);
      await monthlyBudget.UpdateChannel(Context.Guild);

      await _db.SaveChangesAsync();

      //await DeleteOriginalResponseAsync();

      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "";
        msg.Embed = budget.ToEmbed();
      });
    }

    [SlashCommand("rollover", "rolls a budget deficit (or surplus) over to the next month's budget")]
    public async Task RolloverCommand(string budgetName)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, Context.Channel.Name, Context.Guild);
      var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == budgetName.ToLower()).FirstOrDefault();

      if (budget == null)
      {
        await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no budgets named \"{budgetName}\"");
        return;
      }
      await budget.Rollover(_db, Context.Guild);
      await monthlyBudget.UpdateChannel(Context.Guild);

      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "success";
      });
    }

    [Group("transfer", "Subcommand group description")]
    public class BudgetTransferCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public BudgetTransferCommands(IServiceProvider services)
      {
        _db = services.GetRequiredService<BudgetBotEntities>();
      }

      [SlashCommand("bucket", "transfer the surplus (or deficit) budget to a savings bucket")]
      public async Task BucketCommand(string budgetName, string bucketName, decimal amount = 0)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, Context.Channel.Name, Context.Guild);
        var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == budgetName.ToLower()).FirstOrDefault();
        var bucket = await _db.Buckets
          .AsAsyncEnumerable()
          .Where(b => b.Name.ToLower() == budgetName.ToLower())
          .FirstOrDefaultAsync();

        if (budget == null || bucket == null)
        {
          string content = budget == null ? $"budgets named \"{budgetName}\"" : $"savings buckets named \"{bucketName}\"";
          await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no {content}");
          return;
        }

        var transfer = new Transfer()
        {
          Amount = amount == 0 ? budget.AmountRemaining : amount,
          Date = DateTimeOffset.Now,
          OriginalBudgetCategory = budget,
          TargetBucket = bucket
        };

        transfer.Apply();

        await _db.Transfers.AddAsync(transfer);
        await _db.SaveChangesAsync();

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "success";
        });
      }

      [SlashCommand("budget", "transfer the surplus (or deficit) budget to a savings bucket")]
      public async Task BudgetCommand(string currentBudget, string targetBudget, decimal amount = 0)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, Context.Channel.Name, Context.Guild);
        var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == currentBudget.ToLower()).FirstOrDefault();
        var nextBudget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == targetBudget.ToLower()).FirstOrDefault();

        if (budget == null || nextBudget == null)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no budgets named \"{(budget == null ? currentBudget : targetBudget)}\"");
          return;
        }

        if (amount > budget.AmountRemaining)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"The amount entered, ${amount}, is greater than the amount remaining in the budget, {budget.AmountRemaining}");
          return;
        }

        var transfer = new Transfer()
        {
          Amount = amount == 0 ? budget.AmountRemaining : amount,
          Date = DateTimeOffset.Now,
          OriginalBudgetCategory = budget,
          TargetBudgetCategory = nextBudget
        };

        transfer.Apply();

        await _db.Transfers.AddAsync(transfer);
        await _db.SaveChangesAsync();

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "success";
        });
      }
    }

    [SlashCommand("overview", "creates a new budget")]
    public async Task OverviewCommand()
    {
      var sb = new StringBuilder();
      var progressLineFull = "---------------------------------------------------";

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now);
      monthlyBudget.Budgets ??= new List<BudgetCategory>();

      if (monthlyBudget.Budgets == null || monthlyBudget.Budgets.Count == 0)
      {
        sb.AppendLine($"{monthlyBudget.Name} has no categorized budgets.");
        await RespondAsync(sb.ToString());
        return;
      }

      monthlyBudget.Budgets.Sort((b1,b2) => b1.Balance.CompareTo(b2.Balance));
      var maxSpending = monthlyBudget.Budgets[0].Balance;

      //foreach (var budget in monthlyBudget.Budgets)
      //{
      //  sb.AppendLine(budget)
      //}

      //if (monthlyBudget.Budgets.Any(x => x.Name == name))
      //{
      //  sb.AppendLine($"There is already a budget named {name} for {monthlyBudget.Date:Y}");
      //  await RespondAsync(sb.ToString());
      //  return;
      //}

      //monthlyBudget.Budgets.Add(budget);

      //_db.Update(monthlyBudget);
      //_db.SaveChanges();

      //monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now);

      //// send simple string reply
      //await RespondAsync("", new Embed[] { budget.ToEmbed() });
    }
  }
}
