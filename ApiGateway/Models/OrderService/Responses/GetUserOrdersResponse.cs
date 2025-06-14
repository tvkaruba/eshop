using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService.Responses;

public sealed record GetUserOrdersResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonRequired]
    [property: JsonPropertyName("orders")]
    IReadOnlyCollection<OrderDto> Orders,

    [property: JsonRequired]
    [property: JsonPropertyName("totalCount")]
    [Range(0, int.MaxValue)]
    int TotalCount
);
