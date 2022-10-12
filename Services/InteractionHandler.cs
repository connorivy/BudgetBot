using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
//using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Discord.Interactions;

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

    public InteractionHandler(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _config = services.GetRequiredService<IConfiguration>();
      _commands = services.GetRequiredService<InteractionService>();
      _client = services.GetRequiredService<DiscordSocketClient>();
      _logger = services.GetRequiredService<ILogger<InteractionHandler>>();
      _services = services;

      //// take action when we execute a command
      //_commands.CommandExecuted += CommandExecutedAsync;

      //// take action when we receive a message (so we can process it, and see if it is a valid command)
      //_client.MessageReceived += MessageReceivedAsync;
    }

    public async Task InitializeAsync()
    {
      // register modules that are public and inherit ModuleBase<T>.
      await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

      // process the InteractionCreated payloads to execute Interactions commands
      _client.InteractionCreated += HandleInteraction;

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
  }
}