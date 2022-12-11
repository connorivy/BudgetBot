using BudgetBot.Modules;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Database
{
  public abstract class BucketBase
  {
    [Key]
    public long Id { get; set; }
    public string Name { get; set; }
    public decimal Balance { get; set; }
    public decimal StartingAmount { get; set; }
    public decimal TargetAmount { get; set; }
    public string Note { get; set; }
    public List<Transaction> Transactions { get; set; }
    public decimal AbsBalance => Math.Abs(Balance);
    public decimal AbsTargetAmount => Math.Abs(TargetAmount);
    public decimal Progress => Math.Max(Math.Min((Balance - StartingAmount) / (TargetAmount - StartingAmount), 1), 0);

    #region shared methods

    public Embed ToEmbed()
    {
      var sb = new StringBuilder();
      var embed = new EmbedBuilder();

      embed.Title = $"**{Name}** : $**{(int)Math.Round(AmountRemaining)}** left";

      var numCharacters = 28;
      numCharacters -= AbsBalance.ToString().Length + AbsTargetAmount.ToString().Length;

      var numFirstCharacter = (int)Math.Ceiling((Progress) * numCharacters);

      //var progressBar = $"[{new string('=', numFirstCharacter)}${new StringBuilder().Insert(0, " -", numCharacters - numFirstCharacter)}]";

      var progressBar = $"[`{new StringBuilder().Insert(0, "=", numFirstCharacter)}>{new StringBuilder().Insert(0, " ", numCharacters - numFirstCharacter)}`]";

      sb.AppendLine($"${(int)Math.Round(AbsBalance)} {progressBar} ${(int)Math.Round(AbsTargetAmount)}");

      embed.Description = sb.ToString();
      embed.Color = GetColor();

      return embed.Build();
    }
    public Color GetColor()
    {
      var red = 255;
      var green = 0;

      switch (ColorProgress)
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
    public async Task UpdateChannel(BudgetBotEntities _db, SocketGuild guild)
    {
      string channelName = null;

      var embeds = new List<Embed>();
      if (this is Bucket buc)
      {
        channelName = "Buckets";
        var buckets = await _db.Buckets
          .AsAsyncEnumerable()
          .Take(50)
          .ToListAsync();

        foreach (var bucket in buckets)
          embeds.Add(bucket.ToEmbed());
      }
      else if (this is BudgetCategory budg)
      {
        var monthlyBudget = await HelperFunctions.GetExistingMonthlyBudget(_db, budg);
        await monthlyBudget.UpdateChannel(guild);
        return;
      }

      var channelId = await HelperFunctions.GetChannelId(guild, channelName, HelperFunctions.BudgetCategoryName);
      var channel = guild.GetTextChannel(channelId);
      await HelperFunctions.RefreshChannel(embeds, channel);
    }
    #endregion

    #region abstract methods
    public abstract decimal AmountRemaining { get; }
    public abstract Task AddTransaction(BudgetBotEntities _db, SocketGuild guild, Transaction transaction);
    public abstract decimal ColorProgress { get; }
    public abstract int ColorFloor { get; }
    #endregion
  }

  public class Bucket : BucketBase
  {
    public DateTimeOffset TargetDate { get; set; }
    public TimeSpan TimeRemaining => TargetDate - DateTimeOffset.Now;
    public bool IsDebt { get; set; }

    # region overrides
    public override decimal AmountRemaining => TargetAmount - Balance;
    public override async Task AddTransaction(BudgetBotEntities _db, SocketGuild guild, Transaction transaction)
    {
      if (transaction.Bucket != null)
      {
        if (transaction.Bucket == this)
          return;
        transaction.Bucket.Balance -= transaction.Amount;
      }
      else if (transaction.BudgetCategory != null)
        transaction.BudgetCategory.Balance -= transaction.Amount;
      else if (transaction.BudgetCategory == null)
        await transaction.DeleteMessageFromUncategorizedChannel(guild);

      transaction.Bucket = this;
      Balance += transaction.Amount;
      await transaction.AddMessageToCategorizedChannel(guild);
      await _db.SaveChangesAsync();
    }
    public override decimal ColorProgress => Progress;
    public override int ColorFloor => 0;
    #endregion
  }

  public class BudgetCategory : BucketBase
  {
    public long MonthlyBudgetId { get; set; }
    public MonthlyBudgetBase MonthlyBudget { get; set; }
    public bool IsIncome { get; set; }
    public long? TargetBucketId { get; set; }
    public Bucket TargetBucket { get; set; }
    public async Task Rollover(BudgetBotEntities _db, SocketGuild guild)
    {
      if (MonthlyBudget is not MonthlyBudget monthlyBudget)
        return;

      var nextMonth = monthlyBudget.Date.AddMonths(1);

      var nextMonthlyBudget = await HelperFunctions.GetOrCreateMonthlyBudget(_db, nextMonth, guild);
      var nextMonthBudgetCat = nextMonthlyBudget.Budgets.Where(b => b.Name == Name).FirstOrDefault();

      if (nextMonthBudgetCat == null)
      {
        nextMonthBudgetCat = new BudgetCategory()
        {
          Name = Name,
          Balance = 0,
          TargetAmount = TargetAmount,
          StartingAmount = 0,
        };
        nextMonthlyBudget.Budgets.Add(nextMonthBudgetCat);
      }

      var transfer = new Transfer()
      {
        Amount = TargetAmount - Balance,
        Date = DateTimeOffset.Now,
        OriginalBudgetCategory = this,
        TargetBudgetCategory = nextMonthBudgetCat
      };

      transfer.Apply();

      await _db.Transfers.AddAsync(transfer);
      await _db.SaveChangesAsync();
      await UpdateChannel(_db, guild);
      await nextMonthlyBudget.UpdateChannel(guild);
    }
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
    public override decimal AmountRemaining => IsIncome ? TargetAmount - Balance : Balance - TargetAmount;
    public override async Task AddTransaction(BudgetBotEntities _db, SocketGuild guild, Transaction transaction)
    {
      if (transaction.BudgetCategory != null)
      {
        if (transaction.BudgetCategory == this)
          return;
        transaction.BudgetCategory.Balance -= transaction.Amount;
      }
      else if (transaction.Bucket == null)
        await transaction.DeleteMessageFromUncategorizedChannel(guild);

      transaction.BudgetCategory = this;
      Balance += transaction.Amount;
      await transaction.AddMessageToCategorizedChannel(guild);
      await _db.SaveChangesAsync();

      var targetBucket = await HelperFunctions.GetTargetBucket(_db, this);
      if (targetBucket != null)
      {
        if (transaction.Bucket != null)
          throw new InvalidOperationException("Transaction that is targeted to a bucket cannot be applied as a payment");

        transaction.Bucket = targetBucket;
        targetBucket.Balance += transaction.AbsAmount;

        await transaction.Bucket.UpdateChannel(_db, guild);
        await _db.SaveChangesAsync();
      }

      await UpdateChannel(_db, guild);
    }
    public override decimal ColorProgress => IsIncome ? Progress : 1 - Progress;
    public override int ColorFloor => 80; //add an offset between 100% budget and 101% budget colors
    #endregion
  }

  public class PaymentBudget : BudgetCategory
  {
    public long TargetBucketId { get; set; }
    public Bucket TargetBucket { get; set; }
    public async Task AddPayment(BudgetBotEntities _db, SocketGuild guild, Transaction transaction)
    {
      if (transaction.Bucket != null)
        throw new InvalidOperationException("Transaction that is targeted to a bucket cannot be applied as a payment");

      transaction.Bucket = TargetBucket;
      TargetBucket.Balance += transaction.AbsAmount;
      await transaction.Bucket.UpdateChannel(_db, guild);
      await _db.SaveChangesAsync();
    }
  }
}
