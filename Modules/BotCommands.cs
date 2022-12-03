using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BudgetBot.Database;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetBot.Modules
{
  // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
  public class BotCommands : ModuleBase
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    private readonly BudgetBotEntities _db;

    public BotCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
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
      var channelId = await HelperFunctions.GetChannelId(guild, "transactions-uncategorized", HelperFunctions.TransactionCategoryName);

      var channel = guild.GetTextChannel(channelId);

      await channel.SendMessageAsync("", false, transaction.ToEmbed());
    }

    public async Task NotifyOfDeposit(string creditCardEnding, decimal transactionAmount, DateTimeOffset? date)
    {
      await NotifyOfTransaction(creditCardEnding, transactionAmount, null, date);
    }
  }
}