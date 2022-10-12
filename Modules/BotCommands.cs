using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Discord.Interactions;
using BudgetBot.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace BudgetBot.Modules
{
  // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
  public class BotCommands : ModuleBase
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;

    public BotCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();

      var listCommands = new TransactionCommands.ListCommands(_db);
      var catCommands = new TransactionCommands.CategorizeCommands(_db);
    }

    [Command("hello")]
    public async Task HelloCommand()
    {
      // initialize empty string builder for reply
      var sb = new StringBuilder();

      // get user info from the Context
      var user = Context.User;

      // build out the reply
      sb.AppendLine($"You are -> [{user.Username}]");
      sb.AppendLine("I must now say, World!");

      // send simple string reply
      await ReplyAsync(sb.ToString());
    }

    [Command("categorize")]
    public async Task CategorizeCommand(string cat = null)
    {
      var sb = new StringBuilder();

      cat = cat.ToLower();

      if (string.IsNullOrEmpty(cat))
      {
        sb.AppendLine($"Category cannot be empty");
        sb.AppendLine("Categorize command should look like this  -> categorize \"groceries\"");
        await ReplyAsync(sb.ToString());
      }

      var category = await HelperFunctions.GetCategory(_db, cat, DateTimeOffset.Now);

      if (category == null)
      {
        sb.AppendLine($"Could not find category {cat}.");
        sb.AppendLine("Use /budget list to see current budgets or /budget help for other options.");
        await ReplyAsync(sb.ToString());
      }

      // get user info from the Context
      //var user = Context.User;

      var transaction = await HelperFunctions.GetTransaction(_db, Context.Message.ReferencedMessage);
      category.AddTransaction(transaction);
      //await _db.SaveChangesAsync(); //do I need this??

      await ReplyAsync(null, false, category.ToEmbed());
    }

    //public async Task<Bucket> GetCategory(string cat, DateTimeOffset date)
    //{
    //  var budget = await GetMonthlyBudget(date);
    //  var category = budget.Budgets.Where(x => x.Name == cat).FirstOrDefault();

    //  return category;
    //}

    //public async Task<MonthlyBudget> GetMonthlyBudget(DateTimeOffset date)
    //{
    //  var budget = await _db.MonthlyBudgets
    //      .AsQueryable()
    //      .Take(1)
    //      .Where(b => b.Date.Year == date.Year && b.Date.Month == date.Month)
    //      .FirstOrDefaultAsync();

    //  budget ??= await CreateMonthlyBudget(date);
    //  return budget;
    //}

    //public async Task<MonthlyBudget> CreateMonthlyBudget(DateTimeOffset date)
    //{
    //  MonthlyBudget monthlyBudget = null;
    //  var defaultTemplate = await _db.MonthlyBudgetTemplates
    //      .AsQueryable()
    //      .Take(1)
    //      .Where(b => b.IsDefault == true)
    //      .FirstOrDefaultAsync();

    //  var budgetName = $"Budget for {date:MMMM} {date:yyyy}";
    //  var budgetsList = new List<Bucket>();
    //  if (defaultTemplate != null)
    //  {
    //    budgetsList = defaultTemplate.Budgets;
    //    budgetName = defaultTemplate.Name.Replace("MMMM", date.ToString("MMMM")).Replace("yyyy", date.ToString("yyyy"));
    //  }

    //  monthlyBudget = new MonthlyBudget
    //  {
    //    Date = new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 0, 0, 0, date.Offset),
    //    Name = budgetName,
    //    Budgets = budgetsList
    //  };
    //  await _db.AddAsync(monthlyBudget);
    //  await _db.SaveChangesAsync();

    //  return monthlyBudget;
    //}

    //public async Task<Transaction> GetTransaction(IUserMessage message)
    //{
    //  var embeds = message.Embeds.ToList();

    //  if (embeds.Count != 1)
    //    return null;

    //  var titleParts = embeds[0].Title.Split(':');
    //  if (titleParts.Length == 2)
    //  {
    //    var id = long.Parse(titleParts[1].ToString().Trim());
    //    var transaction = await _db.Transactions
    //      .AsQueryable()
    //      .Take(1)
    //      .Where(b => b.Id == id)
    //      .FirstOrDefaultAsync();
    //    return transaction;
    //  }

    //  return null;
    //}

    public async Task NotifyOfTransaction(string creditCardEnding, decimal transactionAmount, string merchant, DateTimeOffset? date)
    {
      try
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
        var channelId = await GetChannel(guild);

        var channel = guild.GetTextChannel(channelId);

        await channel.SendMessageAsync("", false, transaction.ToEmbed());
      }
      catch (Exception ex)
      { }

    }

    public async Task<ulong> GetChannel(SocketGuild guild)
    {
      var channel = guild.Channels.SingleOrDefault(x => x.Name == "budgeting");

      if (channel == null) // there is no channel with the name of 'budgeting'
      {
        // create the channel
        var newChannel = await Context.Guild.CreateTextChannelAsync("budgeting");
        return newChannel.Id;
      }
      else
        return channel.Id;
    }
  }
}