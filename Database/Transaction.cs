using System;
using System.ComponentModel.DataAnnotations;

namespace BudgetBot.Database
{
  public partial class Transaction
  {
    [Key]
    public long Id { get; set; }
    public string PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Merchant { get; set; }
    public DateTimeOffset Date { get; set; }
  }
}
