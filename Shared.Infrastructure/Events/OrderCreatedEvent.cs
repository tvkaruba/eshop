namespace Shared.Infrastructure.Events;

public class OrderCreatedEvent
{
    public string OrderId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public double TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string IdempotencyKey { get; set; } = string.Empty;

    public List<OrderItemEvent> Items { get; set; } = new();
}

public class OrderItemEvent
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public double Price { get; set; }
}
