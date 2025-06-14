using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService.Responses;

public sealed record CreateOrderResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonPropertyName("orderId")]
    Guid? OrderId = null
);
