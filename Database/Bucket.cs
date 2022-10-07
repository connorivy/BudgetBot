using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BudgetBot.Database
{
  public class Bucket
  {
    [Key]
    public long Id { get; set; }
    public string Name { get; set; }
    public decimal Balance { get; set; }
    public decimal TargetAmount { get; set; }
    public string TargetDate { get; set; } //DateTime.Now.ToString("yyyyMM");
    public bool IsBudget { get; set; }
    public string Note { get; set; }
  }
}
