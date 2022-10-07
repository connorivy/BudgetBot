using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BudgetBot.Database
{
  public class Transaction
  {
    [Key]
    public long Id { get; set; }
    public string PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Merchant { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Note { get; set; }
    public Bucket Bucket { get; set; }
  }

  public class Transfer
  {
    [Key]
    public long Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public decimal Amount { get; set; }
    public Bucket OriginalBucket { get; set; }
    public Bucket TargetBucket { get; set; }
  }

  public class MonthlyBudget
  {
    [Key]
    public string YyyyMM { get; set; } //DateTime.Now.ToString("yyyyMM");
    public string Name { get; set; }
    public List<Bucket> Budgets { get; set; }
  }
}
