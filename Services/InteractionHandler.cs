using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Discord.Interactions;
using System.Linq;
using BudgetBot.Modules;
using BudgetBot.Database;
using Discord.Net;
using System.Collections.Generic;

namespace BudgetBot.Services
{
  public class InteractionHandler
  {
    // setup fields to be set later in the constructor
    private readonly IConfiguration _config;
    private readonly InteractionService _commands;
    private readonly DiscordSocketClient _client;
    private readonly IServiceProvider _services;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly BudgetBotEntities _db;

    public InteractionHandler(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _config = services.GetRequiredService<IConfiguration>();
      _commands = services.GetRequiredService<InteractionService>();
      _client = services.GetRequiredService<DiscordSocketClient>();
      _logger = services.GetRequiredService<ILogger<InteractionHandler>>();
      _db = services.GetRequiredService<BudgetBotEntities>();
      _services = services;
    }

    public async Task InitializeAsync()
    {
      // register modules that are public and inherit ModuleBase<T>.
      await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

      // process the InteractionCreated payloads to execute Interactions commands
      _client.InteractionCreated += HandleInteraction;
      _client.SelectMenuExecuted += SelectMenuHandler;
      _client.ButtonExecuted += ButtonHandler;

      // process the command execution results 
      _commands.SlashCommandExecuted += SlashCommandExecuted;
      _commands.ContextCommandExecuted += ContextCommandExecuted;
      _commands.ComponentCommandExecuted += ComponentCommandExecuted;
    }

    private Task ComponentCommandExecuted(ComponentCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
      if (!arg3.IsSuccess)
      {
        switch (arg3.Error)
        {
          case InteractionCommandError.UnmetPrecondition:
            // implement
            break;
          case InteractionCommandError.UnknownCommand:
            // implement
            break;
          case InteractionCommandError.BadArgs:
            // implement
            break;
          case InteractionCommandError.Exception:
            // implement
            break;
          case InteractionCommandError.Unsuccessful:
            // implement
            break;
          default:
            break;
        }
      }

      return Task.CompletedTask;
    }

    private Task ContextCommandExecuted(ContextCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
      if (!arg3.IsSuccess)
      {
        switch (arg3.Error)
        {
          case InteractionCommandError.UnmetPrecondition:
            // implement
            break;
          case InteractionCommandError.UnknownCommand:
            // implement
            break;
          case InteractionCommandError.BadArgs:
            // implement
            break;
          case InteractionCommandError.Exception:
            // implement
            break;
          case InteractionCommandError.Unsuccessful:
            // implement
            break;
          default:
            break;
        }
      }

      return Task.CompletedTask;
    }

    private Task SlashCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
    {
      if (!arg3.IsSuccess)
      {
        switch (arg3.Error)
        {
          case InteractionCommandError.UnmetPrecondition:
            // implement
            break;
          case InteractionCommandError.UnknownCommand:
            // implement
            break;
          case InteractionCommandError.BadArgs:
            // implement
            break;
          case InteractionCommandError.Exception:
            // implement
            break;
          case InteractionCommandError.Unsuccessful:
            // implement
            break;
          default:
            break;
        }
      }

      return Task.CompletedTask;
    }

    //private async Task SlashCommandExecuted(SocketSlashCommand command)
    //{
    //  await Task.Delay(1000);
    //}

    private async Task HandleInteraction(SocketInteraction arg)
    {
      try
      {
        // create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
        var ctx = new SocketInteractionContext(_client, arg);
        await _commands.ExecuteCommandAsync(ctx, _services);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex);
        // if a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
        // response, or at least let the user know that something went wrong during the command execution.
        if (arg.Type == InteractionType.ApplicationCommand)
        {
          await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
      }
    }

    #region menu select commands

    public async Task SelectMenuHandler(SocketMessageComponent arg)
    {
      switch(arg.Data.CustomId)
      {
        case "categorize":
          await CategorizeSelectedOption(arg, arg.Data.Values.FirstOrDefault());
          return;
      }
      var text = string.Join(", ", arg.Data.Values);
      await arg.RespondAsync($"You have selected {text}");
    }

    public async Task CategorizeSelectedOption(SocketMessageComponent arg, string value)
    {
      // acknowlege discord interaction
      await arg.DeferAsync(ephemeral: true);

      SocketGuild guild = null;
      if (arg.GuildId is ulong guildId)
        guild = _client.GetGuild(guildId);

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now, guild);
      var selectedBudget = monthlyBudget.Budgets.Where(b => b.Name == value).FirstOrDefault();
      selectedBudget.AddTransaction(HelperFunctions.SelectedTransaction);

      // send message to transactions-categorized
      var channelId = await HelperFunctions.GetChannelId(guild, "transactions-categorized");
      var channel = guild.GetTextChannel(channelId);
      var botMessage = await HelperFunctions.GetSoloMessage(channel);

      if (botMessage == null)
      {
        await channel.SendMessageAsync("", false, embeds: new Embed[] { HelperFunctions.SelectedTransaction.ToEmbed() });
      }
      else
      {
        var embeds = botMessage.Embeds.ToList();
        embeds.Add(HelperFunctions.SelectedTransaction.ToEmbed());
        await HelperFunctions.RefreshEmbeds(embeds, channel);
      }

      // delete message in current channel
      channelId = await HelperFunctions.GetChannelId(guild, "transactions-uncategorized");
      channel = guild.GetTextChannel(channelId);
      await channel.DeleteMessageAsync(HelperFunctions.TransactionMessage);

      HelperFunctions.SelectedTransaction = null;
      await _db.SaveChangesAsync();
      await monthlyBudget.UpdateChannel(guild);

      await arg.ModifyOriginalResponseAsync(x =>
      {
        x.Content = "";
        x.Embed = selectedBudget.ToEmbed();
        //x.Components = selectedBudget.GetComponents();
      });
    }
    #endregion

    #region button commands
    public async Task ButtonHandler(SocketMessageComponent arg)
    {
      switch (arg.Data.CustomId)
      {
        case "rollover":
          await RolloverCommand(arg);
          return;
      }
      var text = string.Join(", ", arg.Data);
      await arg.RespondAsync($"You have selected {text}");
    }

    public async Task RolloverCommand(SocketMessageComponent arg)
    {
      // acknowlege discord interaction
      await arg.DeferAsync(ephemeral: true);

      var budgetCategory = await HelperFunctions.GetBudgetCategory(_db, arg.Message.Embeds.ToList());
      var nextMonth = budgetCategory.MonthlyBudget.Date.AddMonths(1);

      SocketGuild guild = null;
      if (arg.GuildId is ulong guildId)
        guild = _client.GetGuild(guildId);

      var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, nextMonth, guild);
      var nextMonthBudgetCat = monthlyBudget.Budgets.Where(b => b.Name == budgetCategory.Name).FirstOrDefault();

      if (nextMonthBudgetCat == null)
      {
        nextMonthBudgetCat ??= new BudgetCategory()
        {
          Name = budgetCategory.Name,
          Balance = 0,
          TargetAmount = budgetCategory.TargetAmount
        };
        monthlyBudget.Budgets.Add(nextMonthBudgetCat);
      }

      var transfer = new Transfer()
      {
        Amount = budgetCategory.AmountRemaining,
        Date = DateTimeOffset.Now,
        OriginalBudgetCategory = budgetCategory,
        TargetBudgetCategory = nextMonthBudgetCat
      };

      transfer.Apply();
      //_db.Update(budgetCategory);

      await _db.Transfers.AddAsync(transfer);
      await _db.SaveChangesAsync();

      await arg.ModifyOriginalResponseAsync(x =>
      {
        x.Content = "";
        x.Embeds = new Embed[] { budgetCategory.ToEmbed(), nextMonthBudgetCat.ToEmbed() };
        x.Components = nextMonthBudgetCat.GetComponents();
      });
    }

    #endregion
  }
}