using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.PaymentService.Responses;

/// <summary>
/// 
/// </summary>
/// <param name="Success"></param>
/// <param name="Message"></param>
/// <param name="Balance"></param>
/// <param name="AccountId"></param>
/// <param name="Currency"></param>
public sealed record GetBalanceResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonRequired]
    [property: JsonPropertyName("balance")]
    decimal Balance,

    [property: JsonPropertyName("accountId")]
    Guid? AccountId = null,

    [property: JsonPropertyName("currency")]
    string Currency = "RUB"
);
