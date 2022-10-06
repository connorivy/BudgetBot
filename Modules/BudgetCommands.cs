using BudgetBot.Database;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  [Group("budgets", "Group description")]
  public class BudgetCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;

    public BudgetCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    [SlashCommand("create", "creates a new budget")]
    public async Task CreateCommand()
    {
      var sb = new StringBuilder();

      // get user info from the Context
      var user = Context.User;

      // build out the reply
      sb.AppendLine($"You are -> [{user.Username}]");
      sb.AppendLine("I must now say, World!");

      // send simple string reply
      await RespondAsync(sb.ToString());
    }
  }
}
