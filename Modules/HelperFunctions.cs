using BudgetBot.Database;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  public static class HelperFunctions
  {
    public static Transaction SelectedTransaction { get; set; }
    public static IMessage TransactionMessage { get; set; }
    public static SocketGuild Guild { get; set; }

    public static async Task<ulong> GetChannelId(SocketGuild guild, string name)
    {
      name = name.ToLower().Replace(" ", "-");
      var channel = guild.Channels.SingleOrDefault(x => x.Name == name);

      if (channel == null) // there is no channel with the provided name
      {
        var channelCategoryId = await GetChannelCategory(guild, "BudgetBot");
        // create the channel
        var newChannel = await guild.CreateTextChannelAsync(name, b => b.CategoryId = channelCategoryId);
        return newChannel.Id;
      }
      else
        return channel.Id;
    }

    public static async Task<ulong> GetChannelCategory(SocketGuild guild, string name)
    {
      var cat = guild.CategoryChannels.SingleOrDefault(x => x.Name == name);
      if (cat == null)
      {
        var newChannelCat = await guild.CreateCategoryChannelAsync(name);
        return newChannelCat.Id;
      }
      else
        return cat.Id;
    }

    public async static Task<Bucket> GetExistingBucket(BudgetBotEntities _db, string name, SocketGuild guild = null)
    {
      var bucket = await _db.Buckets
          .AsAsyncEnumerable()
          .Where(b => b.Name == name)
          .FirstOrDefaultAsync();

      //if (bucket == null && guild != null)
      //  bucket = await CreateBucket(_db, name, guild);

      return bucket;
    }

    public async static Task<MonthlyBudget> GetMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date, SocketGuild guild = null)
    {
      // cache the current budget
      //if (date.Month == _currentBudget?.Date.Month && date.Year == _currentBudget?.Date.Year)
      //  return _currentBudget;

      var budget = await GetExistingMonthlyBudget(_db, date);
        
      if (budget == null && guild != null)
        budget = await CreateMonthlyBudget(_db, date, guild);

      //_currentBudget = budget;

      return budget;
    }

    public async static Task<MonthlyBudget> GetMonthlyBudget(BudgetBotEntities _db, string channelName, SocketGuild guild = null)
    {
      DateTime date = DateTime.Today;
      var splitName = channelName.Split('-').ToList();
      if (splitName.Count >= 3)
      {
        var year = DateTime.ParseExact(splitName[splitName.Count - 1], "yyyy", CultureInfo.InvariantCulture).Year;
        var month = DateTime.ParseExact(splitName[splitName.Count - 2], "MMM", CultureInfo.InvariantCulture).Month;
        date = new DateTime(year, month, 1);
      }

      return await GetMonthlyBudget(_db, date, guild);
    }

    public async static Task<MonthlyBudget> GetExistingMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date)
    {
      var budget = await _db.MonthlyBudgets
          .AsAsyncEnumerable()
          .Where(b => b.Date.Year == date.Year && b.Date.Month == date.Month)
          .FirstOrDefaultAsync();

      if (budget != null)
      {
        budget.Budgets = await _db.BudgetCategories
          .AsAsyncEnumerable()
          .Where(b => b.MonthlyBudget == budget)
          .ToListAsync();
      }

      return budget;
    }

    public async static Task<MonthlyBudget> CreateMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date, SocketGuild guild)
    {
      MonthlyBudget monthlyBudget = null;
      var defaultTemplate = await _db.MonthlyBudgetTemplates
          .AsAsyncEnumerable()
          .Take(25)
          .Where(b => b.IsDefault == true)
          .FirstOrDefaultAsync();

      var budgetName = $"Budget {date:MMM} {date:yyyy}";
      var budgetsList = new List<BudgetCategory>();
      if (defaultTemplate != null)
      {
        budgetsList = defaultTemplate.Budgets;
        budgetName = defaultTemplate.Name.Replace("MMM", date.ToString("MMM")).Replace("yyyy", date.ToString("yyyy"));
      }
      else
      {
        var lastMonth = date.AddMonths(-1);
        var lastMonthlyBudget = await GetExistingMonthlyBudget(_db, lastMonth);
        if (lastMonthlyBudget != null && lastMonthlyBudget.Budgets != null)
          foreach (var budget in lastMonthlyBudget.Budgets)
            budgetsList.Add(budget);
      }

      monthlyBudget = new MonthlyBudget
      {
        Date = GetEndOfMonth(date),
        Name = budgetName,
        Budgets = budgetsList
      };

      await _db.AddAsync(monthlyBudget);
      await _db.SaveChangesAsync();

      await monthlyBudget.UpdateChannel(guild);

      return monthlyBudget;
    }

    public static DateTimeOffset GetEndOfMonth(DateTimeOffset date)
    {
      return new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Offset);
    }

    public static async Task<Transaction> GetTransaction(BudgetBotEntities _db, List<IEmbed> embeds)
    {
      if (embeds.Count != 1)
        return null;

      var titleParts = embeds[0].Title.Split(':');
      if (titleParts.Length == 2)
      {
        var id = long.Parse(titleParts[1].Trim());

        var transaction = await _db.Transactions
          .AsAsyncEnumerable()
          .Where(b => b.Id == id)
          .FirstOrDefaultAsync();
        return transaction;
      }

      return null;
    }

    public static async Task<BudgetCategory> GetBudgetCategory(BudgetBotEntities _db, List<Embed> embeds)
    {
      if (embeds.Count != 1)
        return null;

      var titleParts = embeds[0].Title.Split(':');
      if (titleParts.Length == 2)
      {
        var name = titleParts[0].Trim();
        var date = DateTimeOffset.Parse(titleParts[1].Trim());

        var monthlyBudget = await GetMonthlyBudget(_db, date);
        var budgetCategory = monthlyBudget.Budgets.Where(b => b.Name == name).FirstOrDefault();

        return budgetCategory;
      }

      return null;
    }

    public static async Task<RestUserMessage> GetSoloMessage(SocketTextChannel channel)
    {
      var messages = (await channel.GetMessagesAsync(5).FlattenAsync() ?? new List<IMessage>()).ToList();

      if (messages.Count == 1 && messages.First() is RestUserMessage botMessage)
        return botMessage;

      return null;
    }

    public static async Task RefreshEmbeds(List<Embed> embeds, SocketTextChannel channel)
    {
      var botMessage = await GetSoloMessage(channel);

      if (botMessage == null)
      {
        await channel.SendMessageAsync("", false, embeds: embeds.ToArray());
        return;
      }

      await botMessage.ModifyAsync(msg =>
      {
        msg.Content = embeds.Count == 0 ? "Create a budget with \"/budget create\"" : "";
        msg.Embeds = embeds.ToArray();
      });
    }
  }
}
