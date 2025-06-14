using System.ComponentModel.DataAnnotations;

namespace PaymentsService.Models;

public class Transaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid AccountId { get; set; }
    
    public Account Account { get; set; } = null!;
    
    [Required]
    public TransactionType Type { get; set; }
    
    public decimal Amount { get; set; }
    
    public decimal BalanceAfter { get; set; }
    
    public string? OrderId { get; set; }
    
    public string? IdempotencyKey { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string Description { get; set; } = string.Empty;
}

public enum TransactionType
{
    TopUp = 1,
    Charge = 2,
    Refund = 3
}
