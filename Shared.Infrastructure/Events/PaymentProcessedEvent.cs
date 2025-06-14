namespace Shared.Infrastructure.Events;

public class PaymentProcessedEvent
{
    public string OrderId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public double Amount { get; set; }

    public bool Success { get; set; }

    public string? TransactionId { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    public string IdempotencyKey { get; set; } = string.Empty;
}
