using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService.Requests;

public record CreateOrderRequest(
    [property: JsonRequired]
    [property: JsonPropertyName("userId")]
    Guid UserId,

    [property: JsonRequired]
    [property: JsonPropertyName("items")]
    [MinLength(1, ErrorMessage = "At least one order item is required")]
    IReadOnlyCollection<OrderItemDto> Items
);
