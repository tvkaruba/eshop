using System.ComponentModel.DataAnnotations;

namespace Shared.Infrastructure.Models;

public class OutboxEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string EventType { get; set; } = string.Empty;
    
    public string EventData { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public bool IsProcessed { get; set; } = false;
    
    public int RetryCount { get; set; } = 0;
    
    public string? ErrorMessage { get; set; }
}
