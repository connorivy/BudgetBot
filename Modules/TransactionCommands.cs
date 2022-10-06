using BudgetBot.Database;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  [Group("transactions", "Group description")]
  public class TransactionCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;


    public TransactionCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    [SlashCommand("list", "list uncategorized transactions")]
    public async Task ListCommand()
    {
      var sb = new StringBuilder();
      var embed = new EmbedBuilder();

      var transactions = new List <Transaction>();
      try
      {
        transactions = await _db.Transactions.ToListAsync();
      }
      catch (Exception ex)
      {

      }
      if (transactions.Count > 0)
      {
        foreach (var transaction in transactions)
        {
          sb.AppendLine($"{transaction.Amount} {transaction.Merchant}");
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
}
