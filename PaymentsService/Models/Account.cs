using System.ComponentModel.DataAnnotations;

namespace PaymentsService.Models;

public class Account
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public decimal Balance { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public int Version { get; set; } = 0;
    
    public List<Transaction> Transactions { get; set; } = new();
}
