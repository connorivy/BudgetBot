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
    [MessageCommand("rollover")]
    public async Task MessageCommand(IMessage message)
    {
      await RespondAsync($"Author is {message.Author.Username}");
    }
    #endregion

    [SlashCommand("create", "creates a new budget")]
    public async Task CreateCommand(string name, decimal limit, bool isIncome = false)
    {
      var sb = new StringBuilder();

      name = name.ToLower();
      limit = Math.Abs(limit);

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now);
      monthlyBudget.Budgets ??= new List<BudgetCategory>();

      if (monthlyBudget.Budgets.Any(x => x.Name == name))
      {
        sb.AppendLine($"There is already a budget named {name} for {monthlyBudget.Date:Y}");
        await RespondAsync(sb.ToString());
        return;
      }

      var budget = new BudgetCategory
      {
        Balance = 0,
        Name = name,
        TargetAmount = isIncome ? limit * -1 : limit,
        MonthlyBudget = monthlyBudget,
      };

      monthlyBudget.Budgets.Add(budget);

      _db.Update(monthlyBudget);
      _db.SaveChanges();

      monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now);

      // send simple string reply
      await RespondAsync("", new Embed[] { budget.ToEmbed() });
    }
  }
}
