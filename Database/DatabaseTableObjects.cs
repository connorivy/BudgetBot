using BudgetBot.Modules;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Database
{
  public class Transaction
  {
    [Key]
    public long Id { get; set; }
    public string PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Merchant { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Note { get; set; }
    public decimal AbsAmount => Math.Abs(Amount);

    // navigation properties
    public string BucketName { get; set; }
    public Bucket Bucket { get; set; }
    public long? BudgetCategoryId { get; set; }
    public BudgetCategory BudgetCategory { get; set; }
    public async Task DeleteMessageFromUncategorizedChannel(SocketGuild guild)
    {
      var message = await GetMessageFromChannel(guild, "transactions-uncategorized");
      if (message is RestUserMessage msg)
        await msg.DeleteAsync();
    }
    public async Task<IMessage> GetMessageFromChannel(SocketGuild guild, SocketTextChannel channel)
    {
      if (channel.Name == "transactions-uncategorized")
        return await HelperFunctions.GetTransactionMessage(channel, Id);
      else if (channel.Name == "transactions-categorized")
        return await HelperFunctions.GetSoloMessage(channel);
      return null;
    }
    public async Task<IMessage> GetMessageFromChannel(SocketGuild guild, string channelName)
    {
      var channel = await HelperFunctions.GetChannel(guild, channelName);
      return await GetMessageFromChannel(guild, channel);
    }
    public async Task AddMessageToCategorizedChannel(SocketGuild guild)
    {
      var channel = await HelperFunctions.GetChannel(guild, "transactions-categorized");
      var message = await GetMessageFromChannel(guild, channel);
      if (message is RestUserMessage msg)
      {
        var embeds = msg.Embeds.ToList();
        embeds.Add(ToEmbed());
        await HelperFunctions.RefreshEmbeds(embeds, channel);
      }
      else
        await channel.SendMessageAsync("", false, embeds: new Embed[] { ToEmbed() });
    }
    public async Task UpdateChannel(SocketGuild guild)
    {
      if (BudgetCategory != null || Bucket != null)
      {
        var channel = await HelperFunctions.GetChannel(guild, "transactions-categorized");
        var message = await GetMessageFromChannel(guild, "transactions-categorized");

        if (message == null)
          await channel.SendMessageAsync(embed: ToEmbed());
      }
      else
      {
        var message = await GetMessageFromChannel(guild, "transactions-uncategorized");

        if (message is RestUserMessage msg)
          await msg.ModifyAsync(m => m.Embed = ToEmbed());
      }
    }

    public Embed ToEmbed()
    {
      var sb = new StringBuilder();
      var embed = new EmbedBuilder();

      if (Merchant != null)
      {
        embed.Title = $"Transaction : {Id}";
        embed.Color = GetColor();
      }
      else
      {
        embed.Title = $"Deposit : {Id}";
        embed.Color = new Color(0, 0, 255);
      }
      sb.AppendLine($"Payment Method:\t{PaymentMethod}");
      sb.AppendLine($"Amount:\t\t${AbsAmount}");
      sb.AppendLine($"Merchant:\t\t{Merchant}");
      sb.AppendLine($"Date:\t\t{Date:f}");
      if (BudgetCategory != null)
        sb.AppendLine($"Budget:\t\t{BudgetCategory.Name}");
      if (Bucket != null)
        sb.AppendLine($"Bucket:\t\t{Bucket.Name}");
      if (!string.IsNullOrEmpty(Note))
        sb.AppendLine($"Note:\t\t{Note}");

      embed.Description = sb.ToString();

      return embed.Build();
    }

    public Color GetColor()
    {
      var red = 255;
      var green = 0;

      // hard coded for now. Eventually want it based on the standard deviation of the last x db entries
      // for the specified category
      var redDollarAmount = 200; 

      // if maxValue is less than 0, then it is a budget not a bucket and all colors should be flipped
      decimal progress = 0;
      progress = (redDollarAmount - AbsAmount) / 200;

      switch (progress)
      {
        // green
        case decimal n when (n >= 1):
          red = 0;
          green = 255;
          break;
        // red
        case decimal n when (n <= 0):
          red = 255;
          green = 0;
          break;
        // red - yellow
        case decimal n when (n <= (decimal)0.5):
          red = 255;
          green = (int)Math.Round(255 * (2 * n));
          break;
        // yellow - green
        case decimal n when (n < 1):
          green = 255;
          red = (int)Math.Round(255 * 2 * (1 - n));
          break;
      }

      return new Color(red, green, 0);
    }
  }

  public class Transfer
  {
    [Key]
    public long Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public decimal Amount { get; set; }
    public Bucket OriginalBucket { get; set; }
    public Bucket TargetBucket { get; set; }
    public long? OriginalBudgetCategoryId { get; set; }
    public BudgetCategory OriginalBudgetCategory { get; set; }
    public long? TargetBudgetCategoryId { get; set; }
    public BudgetCategory TargetBudgetCategory { get; set; }

    public void Apply()
    {
      if (OriginalBucket != null && TargetBucket != null)
      {
        OriginalBucket.Balance -= Amount;
        TargetBucket.Balance += Amount;
      }
      else if (OriginalBudgetCategory != null && TargetBudgetCategory != null)
      {
        OriginalBudgetCategory.Balance += Amount;
        TargetBudgetCategory.Balance -= Amount;
      }
      else if (OriginalBucket != null && TargetBudgetCategory != null)
      {
        OriginalBucket.Balance -= Amount;
        TargetBudgetCategory.Balance += Amount;
      }
      else if (OriginalBudgetCategory != null && TargetBucket != null)
      {
        OriginalBudgetCategory.Balance -= Amount;
        TargetBucket.Balance += Amount;
      }
    }
    public void Rollback()
    {
      if (OriginalBucket != null && TargetBucket != null)
      {
        OriginalBucket.Balance += Amount;
        TargetBucket.Balance -= Amount;
      }
      else if (OriginalBudgetCategory != null && TargetBudgetCategory != null)
      {
        TargetBudgetCategory.Balance += Amount;
        TargetBudgetCategory.Balance -= Amount;
      }
      else if (OriginalBucket != null && TargetBudgetCategory != null)
      {
        OriginalBucket.Balance += Amount;
        TargetBudgetCategory.Balance -= Amount;
      }
      else if (OriginalBudgetCategory != null && TargetBucket != null)
      {
        OriginalBudgetCategory.Balance += Amount;
        TargetBucket.Balance -= Amount;
      }
    }
  }

  public class MonthlyBudgetBase
  {
    [Key]
    public long Id { get; set; }
    private string _name;
    public string Name
    {
      get => _name;
      set => _name = value.ToLower().Trim().Replace(' ', '-');
    }
    public List<BudgetCategory> Budgets { get; set; }

    public async Task UpdateChannel(SocketGuild guild)
    {
      var channelId = await HelperFunctions.GetChannelId(guild, Name, HelperFunctions.BudgetCategoryName);
      var channel = guild.GetTextChannel(channelId);

      Budgets = Budgets.OrderByDescending(b => b.IsIncome).ThenBy(b => b.Name).ToList();

      var embeds = new List<Embed>();
      foreach (var budget in Budgets)
        embeds.Add(budget.ToEmbed());

      await HelperFunctions.RefreshEmbeds(embeds, channel);
    }
  }
  
  public class MonthlyBudget : MonthlyBudgetBase
  {
    public DateTimeOffset Date { get; set; }
  }

  public class MonthlyBudgetTemplate : MonthlyBudgetBase
  {
    public bool IsDefault { get; private set; } = false;

    public async Task SetDefault(BudgetBotEntities _db)
    {
      var templates = await _db.MonthlyBudgetTemplates
          .AsAsyncEnumerable()
          .ToListAsync();
      foreach (var template in templates)
        template.IsDefault = false;

      IsDefault = true;
      await _db.SaveChangesAsync();
    }
  }
}
