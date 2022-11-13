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
using Discord.Rest;

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
          await RequestCategorizeSelectedOption(arg, arg.Data.Values.FirstOrDefault());
          return;
      }
      var text = string.Join(", ", arg.Data.Values);
      await arg.RespondAsync($"You have selected {text}");
    }

    public async Task RequestCategorizeSelectedOption(SocketMessageComponent arg, string budgetId)
    {
      // acknowlege discord interaction
      await arg.DeferAsync(ephemeral: true);
      long.TryParse(budgetId, out long id);
      if (HelperFunctions.TransactionMessage is SocketUserMessage userMessage)
      {
        var budget = await HelperFunctions.GetBudgetCategory(_db, id);
        await userMessage.ModifyAsync(msg =>
        {
          msg.Content = $"{arg.User.Username} requested to categorize this transaction as {budget.Name}";
          msg.Components = ButtonBuilder(arg.User.Id, HelperFunctions.SelectedTransaction.Id, budgetId);
        });
      }
      await arg.DeleteOriginalResponseAsync();
    }

    public MessageComponent ButtonBuilder(ulong userId, long transactionId, string budgetId)
    {
      var approveBtn = new ButtonBuilder()
      {
        Label = "Approve",
        CustomId = $"categorizeRequest-approve-{userId}-{transactionId}-{budgetId}",
        Style = ButtonStyle.Primary
      };

      var rejectBtn = new ButtonBuilder()
      {
        Label = "Reject",
        CustomId = $"categorizeRequest-reject-{userId}-{transactionId}-{budgetId}",
        Style = ButtonStyle.Secondary
      };

      var builder = new ComponentBuilder()
        .WithButton(approveBtn)
        .WithButton(rejectBtn);

      return builder.Build();
    }

    //public async Task CategorizeSelectedOption(SocketMessageComponent arg, string value)
    //{
    //  // acknowlege discord interaction
    //  await arg.DeferAsync(ephemeral: true);

    //  SocketGuild guild = null;
    //  if (arg.GuildId is ulong guildId)
    //    guild = _client.GetGuild(guildId);

    //  var monthlyBudget = await HelperFunctions.GetMonthlyBudget(_db, DateTimeOffset.Now, guild);
    //  var selectedBudget = monthlyBudget.Budgets.Where(b => b.Name == value).FirstOrDefault();
    //  selectedBudget.AddTransaction(HelperFunctions.SelectedTransaction);

    //  // send message to transactions-categorized
    //  var channelId = await HelperFunctions.GetChannelId(guild, "transactions-categorized", HelperFunctions.TransactionCategoryName);
    //  var channel = guild.GetTextChannel(channelId);
    //  var botMessage = await HelperFunctions.GetSoloMessage(channel);

    //  if (botMessage == null)
    //  {
    //    await channel.SendMessageAsync("", false, embeds: new Embed[] { HelperFunctions.SelectedTransaction.ToEmbed() });
    //  }
    //  else
    //  {
    //    var embeds = botMessage.Embeds.ToList();
    //    embeds.Add(HelperFunctions.SelectedTransaction.ToEmbed());
    //    await HelperFunctions.RefreshEmbeds(embeds, channel);
    //  }

    //  // delete message in current channel
    //  channelId = await HelperFunctions.GetChannelId(guild, "transactions-uncategorized", HelperFunctions.TransactionCategoryName);
    //  channel = guild.GetTextChannel(channelId);
    //  await channel.DeleteMessageAsync(HelperFunctions.TransactionMessage);

    //  HelperFunctions.SelectedTransaction = null;
    //  await _db.SaveChangesAsync();
    //  await monthlyBudget.UpdateChannel(guild);

    //  await arg.ModifyOriginalResponseAsync(x =>
    //  {
    //    x.Content = "";
    //    x.Embed = selectedBudget.ToEmbed();
    //    //x.Components = selectedBudget.GetComponents();
    //  });
    //}
    #endregion

    #region button commands
    public async Task ButtonHandler(SocketMessageComponent arg)
    {
      var splitData = arg.Data.CustomId.Split('-');
      switch (splitData[0])
      {
        //case "rollover":
        //  await RolloverCommand(arg);
        //  return;
        case "categorizeRequest":
          await CategorizeRequest(arg);
          return;
      }
      var text = string.Join(", ", arg.Data);
      await arg.RespondAsync($"You have selected {text}");
    }

    public async Task CategorizeRequest(SocketMessageComponent arg)
    {
      // acknowlege discord interaction
      await arg.DeferAsync(ephemeral: true);

      // categorizeRequest-reject-{userId}-{transactionId}-{budgetId}
      var splitData = arg.Data.CustomId.Split('-');

      var approved = splitData[1] == "approve";
      ulong.TryParse(splitData[2], out ulong userId);
      long.TryParse(splitData[3], out long transactionId);
      long.TryParse(splitData[4], out long budgetId);

      SocketGuild guild = null;
      if (arg.GuildId is ulong guildId)
        guild = _client.GetGuild(guildId);

      if (approved)
      {
        if (userId == arg.User.Id)
        {
          await arg.ModifyOriginalResponseAsync(m =>
          {
            m.Content += $"{new Emoji("\U0001f921")}";
          });
          return;
        }

        var budget = await HelperFunctions.GetBudgetCategory(_db, budgetId);
        var transaction = await HelperFunctions.GetTransaction(_db, transactionId);
        await budget.AddTransaction(guild, transaction);
      }
      else
      {
        var channel = await HelperFunctions.GetChannel(guild, "transactions-uncategorized", HelperFunctions.TransactionCategoryName);
        var message = await HelperFunctions.GetTransactionMessage(channel, transactionId);
        if (message is RestUserMessage msg)
        {
          await msg.ModifyAsync(m =>
          {
            m.Content = "";
            m.Components = null;
          });
        }
      }

      await _db.SaveChangesAsync();
    }

    #endregion
  }
}