using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Discord.Interactions;
using BudgetBot.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace BudgetBot.Modules
{
  // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
  public class BotCommands : ModuleBase
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;

    public BotCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();

      //var listCommands = new TransactionCommands.ListCommands(_db);
      //var catCommands = new TransactionCommands.CategorizeCommands(_db);
    }

    [Command("hello")]
    public async Task HelloCommand()
    {
      // initialize empty string builder for reply
      var sb = new StringBuilder();

      // get user info from the Context
      var user = Context.User;

      // build out the reply
      sb.AppendLine($"You are -> [{user.Username}]");
      sb.AppendLine("I must now say, World!");

      // send simple string reply
      await ReplyAsync(sb.ToString());
    }

    public async Task NotifyOfTransaction(string creditCardEnding, decimal transactionAmount, string merchant, DateTimeOffset? date)
    {
      var transaction = new Transaction
      {
        PaymentMethod = creditCardEnding,
        Amount = transactionAmount,
        Merchant = merchant,
        Date = date ?? DateTimeOffset.Now
      };
      await _db.AddAsync(transaction);
      // save changes to database
      await _db.SaveChangesAsync();

      var guildId = Convert.ToUInt64(_config["TEST_GUILD_ID"]);
      var guild = _client.GetGuild(guildId);
      var channelId = await HelperFunctions.GetChannelId(guild, "transactions-uncategorized");

      var channel = guild.GetTextChannel(channelId);

      await channel.SendMessageAsync("", false, transaction.ToEmbed());
    }
  }
}