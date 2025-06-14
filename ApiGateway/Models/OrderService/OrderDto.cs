using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService;

public sealed record OrderDto(
    [property: JsonRequired]
    [property: JsonPropertyName("orderId")]
    Guid OrderId,

    [property: JsonRequired]
    [property: JsonPropertyName("userId")]
    Guid UserId,

    [property: JsonRequired]
    [property: JsonPropertyName("status")]
    [StringLength(50)]
    string Status,

    [property: JsonRequired]
    [property: JsonPropertyName("totalAmount")]
    [Range(0, double.MaxValue)]
    double TotalAmount,

    [property: JsonRequired]
    [property: JsonPropertyName("createdAt")]
    DateTimeOffset CreatedAt,

    [property: JsonRequired]
    [property: JsonPropertyName("updatedAt")]
    DateTimeOffset UpdatedAt,

    [property: JsonRequired]
    [property: JsonPropertyName("items")]
    IReadOnlyCollection<OrderItemDto> Items,

    [property: JsonPropertyName("paymentTransactionId")]
    Guid? PaymentTransactionId
);
