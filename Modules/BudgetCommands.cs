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
