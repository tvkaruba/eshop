using System.ComponentModel.DataAnnotations;

namespace Shared.Infrastructure.Models;

public class InboxEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string EventType { get; set; } = string.Empty;
    
    public string EventData { get; set; } = string.Empty;
    
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public bool IsProcessed { get; set; } = false;
    
    public string IdempotencyKey { get; set; } = string.Empty;
    
    public int RetryCount { get; set; } = 0;
    
    public string? ErrorMessage { get; set; }
}
