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
  [Group("transaction", "Group description")]
  public class TransactionCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public static BudgetBotEntities _db;


    public TransactionCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    #region message commands
    [MessageCommand("categorize")]
    public async Task CategorizeCommand(IMessage message)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var smb = new SelectMenuBuilder()
        .WithPlaceholder("Categories")
        .WithCustomId("categorize");

      HelperFunctions.SelectedTransaction = await HelperFunctions.GetTransaction(_db, message.Embeds.ToList());
      HelperFunctions.TransactionMessage = message;

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now, Context.Guild);

      if (monthlyBudget.Budgets?.Count == 0)
      {
        await ModifyOriginalResponseAsync(msg => msg.Content = "There are currently no budget categories. Create one with \"/budget create\"");
        return;
      }

      foreach( var budget in monthlyBudget.Budgets)
      {
        smb.AddOption(budget.Name, budget.Name, $"Amount remaining in budget: ${budget.AmountRemaining}");
      }

      var builder = new ComponentBuilder()
        .WithSelectMenu(smb);

      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "Choose a category for this transaction";
        msg.Components = builder.Build();
      });
    }
    #endregion

    [Group("categorize", "Subcommand group description")]
    public class CategorizeCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public CategorizeCommands(BudgetBotEntities db)
      {
        _db = db;
      }

      [SlashCommand("bucket", "flag a recent transaction as a transfer to a bucket")]
      public async Task BucketCommand(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .Where(b => b.Bucket == null)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }
    }

    [Group("list", "Subcommand group description")]
    public class ListCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public ListCommands(BudgetBotEntities db)
      {
        _db = db;
      }
    }
  }
}
