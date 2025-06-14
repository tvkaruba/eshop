using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService.Responses;

public sealed record GetOrderStatusResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,
    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("order")]
    OrderDto? Order = null
);
