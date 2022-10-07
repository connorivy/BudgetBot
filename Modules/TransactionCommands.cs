using BudgetBot.Database;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BudgetBot.Modules
{
  [Group("transactions", "Group description")]
  public class TransactionCommands : InteractionModuleBase<SocketInteractionContext>
  {
    private DiscordSocketClient _client;
    private readonly IConfiguration _config;
    public readonly BudgetBotEntities _db;


    public TransactionCommands(IServiceProvider services)
    {
      // juice up the fields with these services
      // since we passed the services in, we can use GetRequiredService to pass them into the fields set earlier
      _client = services.GetRequiredService<DiscordSocketClient>();
      _config = services.GetRequiredService<IConfiguration>();
      _db = services.GetRequiredService<BudgetBotEntities>();
    }

    [Group("categorize", "Subcommand group description")]
    public class CategorizeCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public CategorizeCommands(BudgetBotEntities db)
      {
        _db = db;
      }

      [SlashCommand("budget", "categorize a recent transaction")]
      public async Task BudgetCommand(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .Where(b => b.Bucket == null)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }

      [SlashCommand("bucket", "flag a recent transaction as a transfer to a bucket")]
      public async Task BucketCommand(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .Where(b => b.Bucket == null)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }
    }

    [Group("list", "Subcommand group description")]
    public class ListCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public ListCommands(BudgetBotEntities db)
      {
        _db = db;
      }

      [SlashCommand("uncategorized", "lists uncategorized transactions")]
      public async Task UncategorizedCommand(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .Where(b => b.Bucket == null)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }

      [SlashCommand("categorized", "lists recent categorized transactions")]
      public async Task CategorizedCommand(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .Where(b => b.Bucket != null)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }

      [SlashCommand("all", "lists recent transactions")]
      public async Task all(int limit = 25)
      {
        var sb = new StringBuilder();
        var embed = new EmbedBuilder();

        var transactions = new List<Transaction>();
        var x = _db.Transactions;

        transactions = await _db.Transactions
          .AsQueryable()
          .Take(limit)
          .ToListAsync();

        if (transactions.Count > 0)
        {
          foreach (var transaction in transactions)
          {
            sb.AppendLine($"${transaction.Amount} {transaction.Merchant}");
          }
        }
        else
        {
          sb.AppendLine("No transactions found!");
        }

        // set embed
        embed.Title = "Transactions";
        embed.Description = sb.ToString();

        // send embed reply
        await RespondAsync("", new Embed[] { embed.Build() });
      }
    }
  }
}
