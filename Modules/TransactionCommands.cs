using BudgetBot.Database;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
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
    private readonly BudgetBotEntities _db;

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

    [Group("categorize", "categorize commands for transactions")]
    public class TransactionCategorizeCommands : InteractionModuleBase<SocketInteractionContext>
    {
      private DiscordSocketClient _client;
      private readonly IConfiguration _config;
      public BudgetBotEntities _db;

      public TransactionCategorizeCommands(IServiceProvider services)
      {
        _client = services.GetRequiredService<DiscordSocketClient>();
        _config = services.GetRequiredService<IConfiguration>();
        _db = services.GetRequiredService<BudgetBotEntities>();
      }

      [SlashCommand("bucket", "categorize a transaction as a withdrawal from a bucket")]
      public async Task BucketCommand(int transactionId, string bucketName)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var transaction = await HelperFunctions.GetTransaction(_db, transactionId);
        var bucket = await HelperFunctions.GetExistingBucket(_db, bucketName, Context.Guild);

        SocketGuild guild = Context.Guild;

        bucket.AddTransaction(transaction);

        // edit embed in buckets
        var channelId = await HelperFunctions.GetChannelId(guild, "transactions-categorized", HelperFunctions.TransactionCategoryName);
        var channel = guild.GetTextChannel(channelId);
        var botMessage = await HelperFunctions.GetSoloMessage(channel);

        if (botMessage == null)
        {
          await channel.SendMessageAsync("", false, embeds: new Embed[] { transaction.ToEmbed() });
        }
        else
        {
          var embeds = botMessage.Embeds.ToList();
          embeds.Add(transaction.ToEmbed());
          await HelperFunctions.RefreshEmbeds(embeds, channel);
        }

        // delete message in transactions-uncategorized channel (if applicable)
        channelId = await HelperFunctions.GetChannelId(guild, "transactions-uncategorized", HelperFunctions.TransactionCategoryName);
        channel = guild.GetTextChannel(channelId);
        var messages = await channel.GetMessagesAsync(50).FlattenAsync();

        IMessage messageInChannel = null;
        foreach (var msg in messages)
        {
          if (!(msg is IMessage m))
            continue;

          if (HelperFunctions.GetTransactionIdFromEmbeds(m.Embeds.ToList()) == transactionId)
          {
            messageInChannel = m;
            break;
          }
        }

        if (messageInChannel != null)
          await channel.DeleteMessageAsync(messageInChannel);

        await _db.SaveChangesAsync();
        await bucket.UpdateChannel(_db, guild);

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = " ";
          msg.Embed = bucket.ToEmbed();
        });
      }
    }
  }
}
