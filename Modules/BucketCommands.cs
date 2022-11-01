using BudgetBot.Database;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.Xml;
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
    public async Task CreateCommand(string name, decimal limit, bool isDebt = false)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var sb = new StringBuilder();

      name = name.ToLower();
      limit = Math.Abs(limit);

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
        Balance = isDebt ? limit * -1 : 0,
        Name = name,
        TargetAmount = isDebt ? 0 : limit,
        IsDebt = isDebt,
        StartingAmount = isDebt ? limit * -1 : 0
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
  }
}
