using Discord;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BudgetBot.Database
{
  public abstract class BucketBase
  {
    public decimal Balance { get; set; }
    public decimal TargetAmount { get; set; }
    public string Note { get; set; }
    public List<Transaction> Transactions { get; set; }
    public decimal AbsBalance => Math.Abs(Balance);
    public decimal AbsTargetAmount => Math.Abs(TargetAmount);

    #region shared methods

    public Embed ToEmbed()
    {
      var sb = new StringBuilder();
      var embed = new EmbedBuilder();

      GetEmbedText(ref embed, ref sb);

      embed.Description = sb.ToString();
      embed.Color = GetColor();

      return embed.Build();
    }
    public Color GetColor()
    {
      var red = 255;
      var green = 0;

      switch (Progress)
      {
        // green
        case decimal n when (n >= 1):
          red = 0;
          green = 255;
          break;
        // red
        case decimal n when (n < 0):
          red = 255;
          green = 0;
          break;
        // red - yellow
        case decimal n when (n <= (decimal)0.5):
          red = 255;
          green = (int)Math.Round((255- ColorFloor) * (2 * n) + ColorFloor); // have a
          break;
        // yellow - green
        case decimal n when (n < 1):
          green = 255;
          red = (int)Math.Round(255 * 2 * (1 - n));
          break;
      }

      return new Color(red, green, 0);
    }
    #endregion

    #region abstract methods
    public abstract decimal AmountRemaining { get; }
    public abstract void AddTransaction(Transaction transaction);
    public abstract void GetEmbedText(ref EmbedBuilder embed, ref StringBuilder sb);
    public abstract decimal Progress { get; }
    public abstract int ColorFloor { get; }
    #endregion
  }

  public class Bucket : BucketBase
  {
    [Key]
    public string Name { get; set; }
    public DateTimeOffset TargetDate { get; set; }
    public TimeSpan TimeRemaining => TargetDate - DateTimeOffset.Now;

    # region overrides
    public override decimal AmountRemaining => TargetAmount - Balance;
    public override void AddTransaction(Transaction transaction)
    {
      transaction.Bucket = this;
      Balance += transaction.Amount;
    }
    public override void GetEmbedText(ref EmbedBuilder embed, ref StringBuilder sb)
    {
      embed.Title = $"Bucket : {Name}";
      sb.AppendLine($"Balance:\t{Balance}");
      sb.AppendLine($"Target Amount:\t{TargetAmount}");
      sb.AppendLine($"Amount Remaining:\t\t{AmountRemaining}");
      sb.AppendLine($"Time Remaining:\t\t{TimeRemaining}");
    }
    public override decimal Progress => Balance / TargetAmount;
    public override int ColorFloor => 0;
    #endregion
  }

  public class BudgetCategory : BucketBase
  {
    [Key]
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTimeOffset MonthlyBudgetDate { get; set; }
    public MonthlyBudget MonthlyBudget { get; set; }
    public MessageComponent GetComponents()
    {
      if (AmountRemaining > 0)
        return null;

      var rolloverButton = new ButtonBuilder()
      {
        Label = "Rollover",
        CustomId = "rollover",
        Style = ButtonStyle.Primary
      };

      var transferButton = new ButtonBuilder()
      {
        Label = "Transfer",
        CustomId = "transfer",
        Style = ButtonStyle.Primary
      };

      var builder = new ComponentBuilder()
        .WithButton(rolloverButton)
        .WithButton(transferButton);

      return builder.Build();
    }

    # region overrides
    public override decimal AmountRemaining => Balance - TargetAmount;
    public override void AddTransaction(Transaction transaction)
    {
      transaction.BudgetCategory = this;
      Balance += transaction.Amount;
    }
    public override void GetEmbedText(ref EmbedBuilder embed, ref StringBuilder sb)
    {
      embed.Title = $"**{Name}** : $**{AmountRemaining}** remaining";

      var numCharacters = 25;
      numCharacters -= AbsBalance.ToString().Length + AbsTargetAmount.ToString().Length;

      var numFirstCharacter = (int)Math.Ceiling(AbsBalance / AbsTargetAmount * numCharacters);
      var progressBar = $"[{new String('=', numFirstCharacter)}>{new StringBuilder().Insert(0, " -", numCharacters - numFirstCharacter)}]";

      sb.AppendLine($"${AbsBalance} {progressBar} ${AbsTargetAmount}");
    }
    public override decimal Progress => (TargetAmount - Balance) / TargetAmount;
    public override int ColorFloor => 80; //add an offset between 100% budget and 101% budget colors
    #endregion
  }
}
