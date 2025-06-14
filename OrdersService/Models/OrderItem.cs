using System.ComponentModel.DataAnnotations;

namespace OrdersService.Models;

public class OrderItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid OrderId { get; set; }
    
    public Order Order { get; set; } = null!;
    
    [Required]
    public string ProductId { get; set; } = string.Empty;
    
    [Required]
    public string ProductName { get; set; } = string.Empty;
    
    public int Quantity { get; set; }
    
    public decimal Price { get; set; }
    
    public decimal TotalPrice => Quantity * Price;
}
