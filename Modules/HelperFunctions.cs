using BudgetBot.Database;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  public static class HelperFunctions
  {
    public static Transaction SelectedTransaction { get; set; }
    public static IMessage TransactionMessage { get; set; }
    public static SocketGuild Guild { get; set; }
    public static string BudgetCategoryName = "Budgets";
    public static string TransactionCategoryName = "Transactions";


    #region channelOperations
    public static async Task<ulong> GetChannelId(SocketGuild guild, string name, string guildCatName = null)
    {
      name = name.ToLower().Replace(" ", "-");
      var channel = guild.Channels.SingleOrDefault(x => x.Name == name);

      if (channel == null) // there is no channel with the provided name
      {
        guildCatName ??= BudgetCategoryName;
        var channelCategoryId = await GetChannelCategory(guild, guildCatName);
        // create the channel
        var newChannel = await guild.CreateTextChannelAsync(name, b => b.CategoryId = channelCategoryId);
        return newChannel.Id;
      }
      else
        return channel.Id;
    }

    public static async Task<SocketTextChannel> GetChannel(SocketGuild guild, string channelName, string guildCatName = null)
    {
      guildCatName ??= BudgetCategoryName;
      var channelId = await GetChannelId(guild, channelName, guildCatName);
      return guild.GetTextChannel(channelId);
    }

    private static async Task<ulong> GetChannelCategory(SocketGuild guild, string name)
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
    #endregion

    #region bucketOperations
    public async static Task<Bucket> GetExistingBucket(BudgetBotEntities _db, string name, SocketGuild guild = null)
    {
      var bucket = await _db.Buckets
          .AsAsyncEnumerable()
          .Where(b => b.Name == name)
          .FirstOrDefaultAsync();

      return bucket;
    }

    public async static Task<Bucket> GetTargetBucket(BudgetBotEntities _db, BudgetCategory budget)
    {
      if (budget.TargetBucket == null)
      {
        if (budget.TargetBucketId == null)
          return null;

        var bucket = await _db.Buckets
          .AsAsyncEnumerable()
          .Where(b => b.Id == budget.TargetBucketId)
          .FirstOrDefaultAsync();
        return bucket;
      }

      if (budget.TargetBucket is Bucket b)
        return b;

      else return null;
    }
    #endregion

    #region monthlyBudgetOperations
    public async static Task<MonthlyBudget> GetOrCreateMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date, SocketGuild guild)
    {
      // cache the current budget
      //if (date.Month == _currentBudget?.Date.Month && date.Year == _currentBudget?.Date.Year)
      //  return _currentBudget;

      var budget = await GetExistingMonthlyBudget(_db, date);
        
      if (budget == null && guild != null)
        budget = await CreateMonthlyBudget(_db, date, guild);
      else
        await budget.UpdateChannel(guild);

      //_currentBudget = budget;

      return budget;
    }

    public async static Task<MonthlyBudget> GetOrCreateMonthlyBudget(BudgetBotEntities _db, string channelName, SocketGuild guild)
    {
      var date = GetDateFromBudgetChannelName(_db, channelName);
      date ??= DateTime.Now;
      
      return await GetOrCreateMonthlyBudget(_db, (DateTime)date, guild);
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

    public async static Task<MonthlyBudget> GetExistingMonthlyBudget(BudgetBotEntities _db, string channelName)
    {
      var date = GetDateFromBudgetChannelName(_db, channelName);
      if (date is DateTime dateTime)
        return await GetExistingMonthlyBudget(_db, dateTime);

      return null;
    }

    public async static Task<MonthlyBudget> GetExistingMonthlyBudget(BudgetBotEntities _db, BudgetCategory budget)
    {
      if (budget.MonthlyBudget == null)
      {
        var monthlyBudget = await _db.MonthlyBudgets
          .AsAsyncEnumerable()
          .Where(b => b.Id == budget.MonthlyBudgetId)
          .FirstOrDefaultAsync();
        return monthlyBudget;
      }
        
      if (budget.MonthlyBudget is MonthlyBudget monthly)
        return monthly;

      else return null;

    }

    public async static Task<MonthlyBudgetBase> GetExistingMonthlyBudgetOrTemplate(BudgetBotEntities _db, string channelName)
    {
      var existingMonthlyBudgets = await GetExistingMonthlyBudget(_db, channelName);

      if (existingMonthlyBudgets != null)
        return existingMonthlyBudgets;


      var date = GetDateFromBudgetChannelName(_db, channelName);
      if (date is DateTime dateTime)
        return await GetExistingMonthlyBudget(_db, dateTime);

      return await GetExistingTemplate(_db, channelName);
    }

    private static DateTime? GetDateFromBudgetChannelName(BudgetBotEntities _db, string channelName)
    {
      DateTime? date = null;
      var splitName = channelName.Split('-').ToList();
      if (splitName.Count >= 3)
      {
        var year = DateTime.ParseExact(splitName[splitName.Count - 1], "yyyy", CultureInfo.InvariantCulture).Year;
        var month = DateTime.ParseExact(splitName[splitName.Count - 2], "MMM", CultureInfo.InvariantCulture).Month;
        date = new DateTime(year, month, 1);
      }

      return date;
    }

    public async static Task<MonthlyBudget> CreateMonthlyBudget(BudgetBotEntities _db, DateTimeOffset date, SocketGuild guild)
    {
      var defaultTemplate = await GetDefaultBudgetTemplate(_db);

      var budgetName = GetChannelNameFromDate(date);
      var budgetsList = new List<BudgetCategory>();
      if (defaultTemplate != null)
      {
        budgetsList = defaultTemplate.Budgets;
        //budgetName = defaultTemplate.Name.Replace("MMM", date.ToString("MMM")).Replace("yyyy", date.ToString("yyyy"));
      }
      else
      {
        var lastMonth = date.AddMonths(-1);
        var lastMonthlyBudget = await GetExistingMonthlyBudget(_db, lastMonth);
        if (lastMonthlyBudget != null && lastMonthlyBudget.Budgets != null)
          foreach (var budget in lastMonthlyBudget.Budgets)
            budgetsList.Add(budget);
      }

      var monthlyBudget = new MonthlyBudget
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

    public static string GetChannelNameFromDate(DateTimeOffset date)
    {
      var budgetName = $"Budget {date:MMM} {date:yyyy}";
      return budgetName.ToLower().Trim().Replace(' ', '-');
    }

    #endregion

    #region monthlyBudgetTemplateOperations
    public async static Task<MonthlyBudgetTemplate> GetDefaultBudgetTemplate(BudgetBotEntities _db)
    {
      var defaultTemplate = await _db.MonthlyBudgetTemplates
          .AsAsyncEnumerable()
          .Take(25)
          .Where(b => b.IsDefault == true)
          .FirstOrDefaultAsync();

      return defaultTemplate;
    }

    #endregion

    #region transactionOperations
    public static async Task<Transaction> GetTransaction(BudgetBotEntities _db, List<IEmbed> embeds)
    {
      var transactionId = GetTransactionIdFromEmbeds(embeds);
      if (!(transactionId is long id))
        return null;

      var transaction = await GetTransaction(_db, id);
      return transaction;
    }

    public static long? GetTransactionIdFromEmbeds(List<IEmbed> embeds)
    {
      if (embeds.Count != 1)
        return null;

      var titleParts = embeds[0].Title.Split(':');
      if (titleParts.Length == 2)
      {
        var id = long.Parse(titleParts[1].Trim());
        return id;
      }
      return null;
    }

    public static async Task<Transaction> GetTransaction(BudgetBotEntities _db, long transactionId)
    {
      var transaction = await _db.Transactions
        .AsAsyncEnumerable()
        .Where(b => b.Id == transactionId)
        .FirstOrDefaultAsync();
      return transaction;
    }

    public static async Task<IMessage> GetTransactionMessage(SocketTextChannel channel, long id)
    {
      var messages = (await channel.GetMessagesAsync(50).FlattenAsync() ?? new List<IMessage>()).ToList();

      foreach (var message in messages)
      {
        var messageId = GetTransactionIdFromEmbeds(message.Embeds.ToList());
        if (messageId == id)
          return message;
      }

      return null;
    }

    #endregion

    #region budgetCategoryOperations
    public static async Task<BudgetCategory> GetBudgetCategory(BudgetBotEntities _db, long budgetId)
    {
      var budget = await _db.BudgetCategories
        .AsAsyncEnumerable()
        .Where(b => b.Id == budgetId)
        .FirstOrDefaultAsync();
      return budget;
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

        var monthlyBudget = await GetExistingMonthlyBudget(_db, date);
        var budgetCategory = monthlyBudget.Budgets.Where(b => b.Name == name).FirstOrDefault();

        return budgetCategory;
      }

      return null;
    }

    #endregion

    #region templateOperations

    public async static Task<MonthlyBudgetTemplate> GetExistingTemplate(BudgetBotEntities _db, string channelName)
    {
      var template = await _db.MonthlyBudgetTemplates
          .AsAsyncEnumerable()
          .Where(b => b.Name == channelName)
          .FirstOrDefaultAsync();

      return template;
    }

    #endregion

    #region miscOperations

    public static DateTimeOffset GetEndOfMonth(DateTimeOffset date)
    {
      return new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Offset);
    }

    public static async Task<IEnumerable<IMessage>> GetMessages(SocketTextChannel channel)
    {
      return await channel.GetMessagesAsync().FlattenAsync() ?? new List<IMessage>();
    }

    public static async Task<RestUserMessage> GetSoloMessage(SocketTextChannel channel)
    {
      var messages = (await channel.GetMessagesAsync(5).FlattenAsync() ?? new List<IMessage>()).ToList();

      if (messages.Count == 1 && messages.First() is RestUserMessage botMessage)
      {
        if (botMessage.Embeds == null || botMessage.Embeds.Count == 0)
        {
          await botMessage.DeleteAsync();
          return null;
        }
        return botMessage;
      }

      return null;
    }

    public static async Task RefreshEmbeds(List<Embed> embeds, SocketTextChannel channel)
    {
      var messages = await GetMessages(channel);

      // to avoid annoying reappearance of bot profile when sending messages later
      // delete all messages and resend them if there aren't already enough messages in the channel
      if (embeds.Count > messages.Count())
      {
        foreach (var msg in messages)
          await msg.DeleteAsync();
        foreach (var embed in embeds)
          await channel.SendMessageAsync(embed: embed);
      }

      // however, the above process is a bit expensive, so don't do it if you don't have to
      else
      {
        for (int i = 0; i < embeds.Count; i++)
          if (messages.ElementAt(i) is RestUserMessage msg)
            await msg.ModifyAsync(m =>
            {
              m.Embed = embeds[i];
            });

        for (int i = embeds.Count; i < messages.Count(); i++)
          if (messages.ElementAt(i) is RestUserMessage msg)
            await msg.DeleteAsync();
      }
    }

    public static async Task RefreshEmbeds(List<Embed> embeds, SocketTextChannel channel, RestUserMessage botMessage)
    {
      if (botMessage == null)
      {
        if (embeds.Count == 0)
          await channel.SendMessageAsync("Welcome to your budget");
        else
          await channel.SendMessageAsync("", false, embeds: embeds.ToArray());
        return;
      }

      if (embeds == null || embeds.Count == 0)
        await botMessage.DeleteAsync();

      await botMessage.ModifyAsync(msg =>
      {
        msg.Content = embeds.Count == 0 ? "Create a budget with \"/budget create\"" : "";
        msg.Embeds = embeds.ToArray();
      });
    }

    #endregion
  }
}
