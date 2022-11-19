using System;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using BudgetBot.Services;
using System.Linq;
using Serilog;
using Discord.Interactions;
using BudgetBot.Modules;
using BudgetBot.Database;

namespace BudgetBot
{
  class Program
  {
    // setup our fields we assign later
    private readonly IConfiguration _config;
    private DiscordSocketClient _client;
    private static string _logLevel;
    private InteractionService _interactions;
    private ulong _testGuildId;

    static void Main(string[] args = null)
    {
      if (args.Count() != 0)
      {
        _logLevel = args[0];
      }
      Log.Logger = new LoggerConfiguration()
          .WriteTo.File("logs/BudgetBot.log", rollingInterval: RollingInterval.Day)
          .WriteTo.Console()
          .CreateLogger();

      new Program().MainAsync().GetAwaiter().GetResult();
    }

    public Program()
    {
      // create the configuration
      var _builder = new ConfigurationBuilder()
          .SetBasePath(AppContext.BaseDirectory)
          .AddJsonFile(path: "config.json");

      // build the configuration and assign to _config          
      _config = _builder.Build();
      _testGuildId = ulong.Parse(_config["TEST_GUILD_ID"]);
    }

    public async Task MainAsync()
    {
      // call ConfigureServices to create the ServiceCollection/Provider for passing around the services
      using (var services = ConfigureServices())
      {
        // get the client and assign to client 
        // you get the services via GetRequiredService<T>
        var client = services.GetRequiredService<DiscordSocketClient>();
        _client = client;

        var interactions = services.GetRequiredService<InteractionService>();
        _interactions = interactions;

        // setup logging and the ready event
        services.GetRequiredService<LoggingService>();
        client.Ready += ReadyAsync;

        // this is where we get the Token value from the configuration file, and start the bot
        await client.LoginAsync(TokenType.Bot, _config["BOT_TOKEN"]);
        await client.StartAsync();

        await services.GetRequiredService<InteractionHandler>().InitializeAsync();
        await services.GetRequiredService<MailListener>().InitializeAsync();

        await Task.Delay(-1);
      }
    }

    private Task LogAsync(LogMessage log)
    {
      Console.WriteLine(log.ToString());
      return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
      if (IsDebug())
      {
        // this is where you put the id of the test discord guild
        System.Console.WriteLine($"In debug mode, adding commands to {_testGuildId}...");
        await _interactions.RegisterCommandsToGuildAsync(_testGuildId);
      }
      else
      {
        // this method will add commands globally, but can take around an hour
        await _interactions.RegisterCommandsGloballyAsync(true);
      }
      Console.WriteLine($"Connected as -> [{_client.CurrentUser}] :)");
    }

    // this method handles the ServiceCollection creation/configuration, and builds out the service provider we can call on later
    private ServiceProvider ConfigureServices()
    {
      // this returns a ServiceProvider that is used later to call for those services
      // we can add types we have access to here, hence adding the new using statement:
      // the config we build is also added, which comes in handy for setting the command prefix!
      var services = new ServiceCollection()
        .AddSingleton(_config)
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
        .AddSingleton<InteractionHandler>()
        .AddSingleton<LoggingService>()
        .AddScoped<BotCommands>()
        .AddScoped<BucketCommands>()
        .AddScoped<TransactionCommands>()
        .AddScoped<TransactionCommands.TransactionCategorizeCommands>()
        .AddScoped<BudgetCommands>()
        .AddScoped<BudgetCommands.BudgetTransferCommands>()
        //.AddScoped<BudgetCommands.BudgetCreateCommands>()
        .AddSingleton<MailListener>()
        .AddDbContext<BudgetBotEntities>()
        .AddLogging(configure => configure.AddSerilog());

      if (!string.IsNullOrEmpty(_logLevel))
      {
        switch (_logLevel.ToLower())
        {
          case "info":
            {
              services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
              break;
            }
          case "error":
            {
              services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
              break;
            }
          case "debug":
            {
              services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
              break;
            }
          default:
            {
              services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
              break;
            }
        }
      }
      else
      {
        services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
      }

      var serviceProvider = services.BuildServiceProvider();
      return serviceProvider;
    }

    static bool IsDebug()
    {
#if DEBUG
      return true;
#else
      return false;
#endif
    }

  }
}