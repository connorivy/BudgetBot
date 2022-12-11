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
using System.Reflection.Metadata;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static BudgetBot.Database.BudgetCategory;

namespace BudgetBot.Modules
{
  [Group("budget", "Group description")]
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

    #region message commands

    #endregion

    [Group("create", "Subcommand group description")]
    public class BudgetCreateCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public BudgetCreateCommands(IServiceProvider services)
      {
        _db = services.GetRequiredService<BudgetBotEntities>();
      }

      [SlashCommand("standard", "add a new budget to the current monthly budget channel")]
      public async Task StandardCommand(string name, decimal limit)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        await CreateBudget(name, limit);
      }

      [SlashCommand("income", "add an income budget to the current monthly budget channel")]
      public async Task IncomeCommand(string name, decimal limit)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        await CreateBudget(name, limit, isIncome: true);
      }

      [SlashCommand("payment", "add a new budget that is a payment to a bucket to the current monthly budget channel")]
      public async Task PaymentCommand(string name, decimal limit, string targetBucket)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        await CreateBudget(name, limit, targetBucket: targetBucket);
      }

      [SlashCommand("monthly", "create a budget for this month")]
      public async Task MonthlyCommand()
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var sb = new StringBuilder();

        var monthlyBudget = await HelperFunctions.GetOrCreateMonthlyBudget(_db, Context.Channel.Name, Context.Guild);
        await _db.SaveChangesAsync();
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "success";
        });
      }

      [SlashCommand("template", "creates a new monthly budget template")]
      public async Task TemplateCommand(string name, bool useCurrentBudget, bool isDefault)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var sb = new StringBuilder();

        name = name.ToLower();
        var channel = await HelperFunctions.GetChannel(Context.Guild, name, HelperFunctions.BudgetCategoryName);
        var template = new MonthlyBudgetTemplate()
        {
          Name = name,
          Budgets = new List<BudgetCategory>()
        };

        if (isDefault)
          await template.SetDefault(_db);

        var embeds = new List<Embed>();

        if (useCurrentBudget)
        {
          var currentMonthly = await HelperFunctions.GetExistingMonthlyBudget(_db, Context.Channel.Name);
          template.Budgets = currentMonthly.Budgets;
          foreach (var budget in template.Budgets)
          {
            budget.Balance = budget.StartingAmount;
            embeds.Add(budget.ToEmbed());
          }
        }

        await _db.MonthlyBudgetTemplates.AddAsync(template);
        await _db.SaveChangesAsync();

        await HelperFunctions.RefreshChannel(embeds, channel);
      }

      #region non-command methods
      public async Task CreateBudget(string name, decimal limit, bool isIncome = false, string targetBucket = null)
      {
        var sb = new StringBuilder();

        name = name.ToLower();
        limit = Math.Abs(limit);

        var monthlyBudget = await HelperFunctions.GetExistingMonthlyBudgetOrTemplate(_db, Context.Channel.Name);

        if (monthlyBudget == null)
        {
          sb.AppendLine($"This command is not valid in this channel.");
          await ModifyOriginalResponseAsync(msg =>
          {
            msg.Content = sb.ToString();
          });
          return;
        }
        monthlyBudget.Budgets ??= new List<BudgetCategory>();

        if (monthlyBudget.Budgets.Any(x => x.Name == name))
        {
          sb.AppendLine($"There is already a budget named {name} in this channel");
          await ModifyOriginalResponseAsync(msg =>
          {
            msg.Content = sb.ToString();
          });
          return;
        }

        var budget = new BudgetCategory
        {
          Balance = 0,
          StartingAmount = 0,
          Name = name,
          IsIncome = isIncome,
          TargetAmount = isIncome ? limit : limit * -1,
          MonthlyBudget = monthlyBudget,
        };

        if (!string.IsNullOrEmpty(targetBucket))
        {
          var bucket = await HelperFunctions.GetExistingBucket(_db, targetBucket);
          if (bucket == null)
          {
            sb.AppendLine($"There is no bucket named {targetBucket}");
            await ModifyOriginalResponseAsync(msg =>
            {
              msg.Content = sb.ToString();
            });
            return;
          }

          budget.TargetBucket = bucket;
        }

        monthlyBudget.Budgets.Add(budget);

        await _db.SaveChangesAsync();
        await monthlyBudget.UpdateChannel(Context.Guild);

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "";
          msg.Embed = budget.ToEmbed();
        });
      }
      #endregion
    }

    [SlashCommand("edit", "edit an existing budget")]
    public async Task EditCommand(string budgetName, decimal? limit = null, bool? isIncome = null, bool editTemplate = false)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var sb = new StringBuilder();

      budgetName = budgetName.ToLower();
      if (limit != null)
        limit = Math.Abs((decimal)limit);

      var monthlyBudget = await HelperFunctions.GetExistingMonthlyBudget(_db, Context.Channel.Name);

      if (monthlyBudget == null)
      {
        sb.AppendLine($"This command is not valid in this channel.");
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = sb.ToString();
        });
        return;
      }
      monthlyBudget.Budgets ??= new List<BudgetCategory>();

      var budget = monthlyBudget.Budgets.Where(b => b.Name == budgetName).FirstOrDefault();
      if (budget == null)
      {
        sb.AppendLine($"There is no budget named {budgetName} for {monthlyBudget.Date:Y}");
        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = sb.ToString();
        });
        return;
      }

      if (editTemplate)
      {
        var template = await HelperFunctions.GetDefaultBudgetTemplate(_db);
        if (template == null)
        {
          sb.AppendLine($"There is no default budget template");
          await ModifyOriginalResponseAsync(msg =>
          {
            msg.Content = sb.ToString();
          });
          return;
        }

        var templateBudget = template.Budgets.Where(b => b.Name == budgetName).FirstOrDefault();
        if (templateBudget == null)
        {
          sb.AppendLine($"There is no budget named {budgetName} in the default budget template, {template.Name}");
          await ModifyOriginalResponseAsync(msg =>
          {
            msg.Content = sb.ToString();
          });
          return;
        }

        EditBudget(templateBudget, limit, isIncome);
      }

      EditBudget(budget, limit, isIncome);

      await _db.SaveChangesAsync();
      await monthlyBudget.UpdateChannel(Context.Guild);
      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "";
        msg.Embed = budget.ToEmbed();
      });
    }

    private void EditBudget(BudgetCategory budget, decimal? limit = null, bool? isIncome = null)
    {
      if (isIncome != null)
        budget.IsIncome = (bool)isIncome;
      else
        isIncome = budget.IsIncome;

      if (limit != null)
        budget.TargetAmount = (bool)isIncome ? (decimal)limit : (decimal)limit * -1;
    }

    [SlashCommand("rollover", "rolls a budget deficit (or surplus) over to the next month's budget")]
    public async Task RolloverCommand(string budgetName)
    {
      // acknowlege discord interaction
      await DeferAsync(ephemeral: true);

      var monthlyBudget = await HelperFunctions.GetOrCreateMonthlyBudget(_db, Context.Channel.Name, Context.Guild);
      var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == budgetName.ToLower()).FirstOrDefault();

      if (budget == null)
      {
        await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no budgets named \"{budgetName}\"");
        return;
      }
      await budget.Rollover(_db, Context.Guild);
      await monthlyBudget.UpdateChannel(Context.Guild);

      await ModifyOriginalResponseAsync(msg =>
      {
        msg.Content = "success";
      });
    }

    [Group("transfer", "Subcommand group description")]
    public class BudgetTransferCommands : InteractionModuleBase<SocketInteractionContext>
    {
      public readonly BudgetBotEntities _db;
      public BudgetTransferCommands(IServiceProvider services)
      {
        _db = services.GetRequiredService<BudgetBotEntities>();
      }

      [SlashCommand("bucket", "transfer the surplus (or deficit) budget to a savings bucket")]
      public async Task BucketCommand(string budgetName, string bucketName, decimal amount = 0)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var monthlyBudget = await HelperFunctions.GetExistingMonthlyBudget(_db, Context.Channel.Name);

        if (monthlyBudget == null)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"This command is not valid in this channel");
          return;
        }

        var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == budgetName.ToLower()).FirstOrDefault();
        var bucket = await _db.Buckets
          .AsAsyncEnumerable()
          .Where(b => b.Name.ToLower() == bucketName.ToLower())
          .FirstOrDefaultAsync();

        if (budget == null || bucket == null)
        {
          string content = budget == null ? $"budgets named \"{budgetName}\"" : $"savings buckets named \"{bucketName}\"";
          await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no {content}");
          return;
        }

        var transfer = new Transfer()
        {
          Amount = amount == 0 ? budget.AmountRemaining : amount,
          Date = DateTimeOffset.Now,
          OriginalBudgetCategory = budget,
          TargetBucket = bucket
        };

        transfer.Apply();

        await _db.Transfers.AddAsync(transfer);
        await _db.SaveChangesAsync();
        await monthlyBudget.UpdateChannel(Context.Guild);
        await bucket.UpdateChannel(_db, Context.Guild);

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "success";
        });
      }

      [SlashCommand("budget", "transfer the surplus (or deficit) budget to a savings bucket")]
      public async Task BudgetCommand(string currentBudget, string targetBudget, decimal amount = 0)
      {
        // acknowlege discord interaction
        await DeferAsync(ephemeral: true);

        var monthlyBudget = await HelperFunctions.GetExistingMonthlyBudget(_db, Context.Channel.Name);
        if (monthlyBudget == null)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"This command is not valid in this channel");
          return;
        }

        var budget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == currentBudget.ToLower()).FirstOrDefault();
        var nextBudget = monthlyBudget.Budgets.Where(b => b.Name.ToLower() == targetBudget.ToLower()).FirstOrDefault();

        if (budget == null || nextBudget == null)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"There are no budgets named \"{(budget == null ? currentBudget : targetBudget)}\"");
          return;
        }

        if (amount > budget.AmountRemaining)
        {
          await ModifyOriginalResponseAsync(msg => msg.Content = $"The amount entered, ${amount}, is greater than the amount remaining in the budget, {budget.AmountRemaining}");
          return;
        }

        var transfer = new Transfer()
        {
          Amount = amount == 0 ? budget.AmountRemaining : amount,
          Date = DateTimeOffset.Now,
          OriginalBudgetCategory = budget,
          TargetBudgetCategory = nextBudget
        };

        transfer.Apply();

        await _db.Transfers.AddAsync(transfer);
        await _db.SaveChangesAsync();

        await ModifyOriginalResponseAsync(msg =>
        {
          msg.Content = "success";
        });
      }
    }
  }
}
