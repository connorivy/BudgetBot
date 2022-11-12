using BudgetBot.Database;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  [Group("bucket", "Group description")]
  public class BucketCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;

    public BucketCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    [SlashCommand("create", "creates a new bucket")]
    public async Task CreateCommand(string name, decimal amount, bool isDebt = false)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var sb = new StringBuilder();

      name = name.ToLower();
      amount = Math.Abs(amount);

      var bucket = await HelperFunctions.GetExistingBucket(_db, name, Context.Guild);

      if (bucket != null)
      {
        sb.AppendLine($"There is already a bucket named {name}");
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = sb.ToString();
        });
        return;
      }

      bucket = new Bucket
      {
        Balance = isDebt ? amount * -1 : 0,
        Name = name,
        TargetAmount = isDebt ? 0 : amount,
        IsDebt = isDebt,
        StartingAmount = isDebt ? amount * -1 : 0
      };

      await _db.Buckets.AddAsync(bucket);
      await _db.SaveChangesAsync();
      await bucket.UpdateChannel(_db, Context.Guild);
      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "";
        msg.Embed = bucket.ToEmbed();
      });
    }

    [SlashCommand("edit", "edit an existing bucket")]
    public async Task EditCommand(string bucketName, decimal amount)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var sb = new StringBuilder();

      bucketName = bucketName.ToLower();

      var bucket = await HelperFunctions.GetExistingBucket(_db, bucketName, Context.Guild);

      if (bucket == null)
      {
        sb.AppendLine($"There is no bucket named {bucketName}");
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = sb.ToString();
        });
        return;
      }

      bucket.TargetAmount = bucket.IsDebt ? 0 : amount;
      bucket.StartingAmount = bucket.IsDebt ? amount * -1 : 0;

      await _db.SaveChangesAsync();
      await bucket.UpdateChannel(_db, Context.Guild);
      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "";
        msg.Embed = bucket.ToEmbed();
      });
    }
  }
}
