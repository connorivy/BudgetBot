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
using BudgetBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static Org.BouncyCastle.Math.EC.ECCurve;
using System.Windows.Input;

namespace BudgetBot.Modules
{
  // for commands to be available, and have the Context passed to them, we must inherit ModuleBase
  public class BotCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;

    public BotCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
    }

    [SlashCommand("hello", "say hello")]
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
      await RespondAsync(sb.ToString());
    }

    [SlashCommand("8ball", "find your answer!")]
    [Discord.Interactions.RequireUserPermission(GuildPermission.KickMembers)]
    public async Task AskEightBall(string question)
    {
      // I like using StringBuilder to build out the reply
      var sb = new StringBuilder();
      // let's use an embed for this one!
      var embed = new EmbedBuilder();

      // now to create a list of possible replies
      var replies = new List<string>();

      // add our possible replies
      replies.Add("yes");
      replies.Add("no");
      replies.Add("maybe");
      replies.Add("hazzzzy....");

      // time to add some options to the embed (like color and title)
      embed.WithColor(new Color(0, 255, 0));
      embed.Title = "Welcome to the 8-ball!";

      // we can get lots of information from the Context that is passed into the commands
      // here I'm setting up the preface with the user's name and a comma
      sb.AppendLine($"{Context.User.Username},");
      sb.AppendLine();

      // let's make sure the supplied question isn't null 
      if (question == null)
      {
        // if no question is asked (question are null), reply with the below text
        sb.AppendLine("Sorry, can't answer a question you didn't ask!");
      }
      else
      {
        // if we have a question, let's give an answer!
        // get a random number to index our list with (arrays start at zero so we subtract 1 from the count)
        var answer = replies[new Random().Next(replies.Count - 1)];

        // build out our reply with the handy StringBuilder
        sb.AppendLine($"You asked: [**{question}**]...");
        sb.AppendLine();
        sb.AppendLine($"...your answer is [**{answer}**]");

        // bonus - let's switch out the reply and change the color based on it
        switch (answer)
        {
          case "yes":
            {
              embed.WithColor(new Color(0, 255, 0));
              break;
            }
          case "no":
            {
              embed.WithColor(new Color(255, 0, 0));
              break;
            }
          case "maybe":
            {
              embed.WithColor(new Color(255, 255, 0));
              break;
            }
          case "hazzzzy....":
            {
              embed.WithColor(new Color(255, 0, 255));
              break;
            }
        }
      }

      // now we can assign the description of the embed to the contents of the StringBuilder we created
      embed.Description = sb.ToString();

      //Embed[] x = new Embed[] { embed.Build() };

      // this will reply with the embed
      await RespondAsync("", new Embed[] { embed.Build() });
      //await ReplyAsync(null, false, embed.Build());
    }

    public async Task NotifyOfTransaction(string creditCardEnding, string transactionAmount, string merchant, DateTimeOffset? date)
    {
      try
      {
        var guildId = Convert.ToUInt64(_config["TEST_GUILD_ID"]);
        var guild = _client.GetGuild(guildId);
        var channelId = await GetChannel(guild);

        var channel = guild.GetTextChannel(channelId);

        await channel.SendMessageAsync($"NEW TRANSACTION! \nCredit Card Ending:\t{creditCardEnding}\nAmount:\t{transactionAmount}\nMerchant:\t{merchant}\nDate:\t{date}");
      }
      catch (Exception ex)
      { }

    }

    public async Task<ulong> GetChannel(SocketGuild guild)
    {
      var channel = guild.Channels.SingleOrDefault(x => x.Name == "budgeting");

      if (channel == null) // there is no channel with the name of 'log'
      {
        // create the channel
        var newChannel = await Context.Guild.CreateTextChannelAsync("log");
        return newChannel.Id;
      }
      else
        return channel.Id;
    }
  }
}