using BudgetBot.Modules;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using MailKit.Search;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Channels;
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

    public Embed ToEmbed()
    {
      var sb = new StringBuilder();
      var embed = new EmbedBuilder();

      embed.Title = $"Transaction : {Id}";
      sb.AppendLine($"Payment Method:\t{PaymentMethod}");
      sb.AppendLine($"Amount:\t\t${AbsAmount}");
      sb.AppendLine($"Merchant:\t\t{Merchant}");
      sb.AppendLine($"Date:\t\t{Date:f}");
      embed.Description = sb.ToString();
      embed.Color = GetColor();

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
        OriginalBudgetCategory.Balance -= Amount;
        TargetBudgetCategory.Balance += Amount;
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
    }
  }

  public class MonthlyBudget
  {
    [Key]
    public DateTimeOffset Date { get; set; }
    private string _name;
    public string Name 
    { 
      get => _name;
      set => _name = value.ToLower().Replace(' ', '-'); 
    }
    public List<BudgetCategory> Budgets { get; set; }

    public async Task UpdateChannel(SocketGuild guild)
    {
      var channelId = await HelperFunctions.GetChannelId(guild, Name);
      var channel = guild.GetTextChannel(channelId);

      Budgets.Sort((b1, b2) => b1.Name.CompareTo(b2.Name));

      var embeds = new List<Embed>();

      foreach (var budget in Budgets)
        embeds.Add(budget.ToEmbed());

      var messages = (await channel.GetMessagesAsync(5).FlattenAsync()).ToList();

      if (messages.Count == 0)
        await channel.SendMessageAsync("", false, embeds: embeds.ToArray());
      else if (messages.Count != 1)
        return;

      if (messages.First() is RestUserMessage botMessage)
      {
        await botMessage.ModifyAsync(msg =>
        {
          msg.Content = "";
          msg.Embeds = embeds.ToArray();
        });
      }
    }
  }

  public class MonthlyBudgetTemplate
  {
    [Key]
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public List<BudgetCategory> Budgets { get; set; }
  }
}
