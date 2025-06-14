using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.PaymentService.Requests;

/// <summary>
/// 
/// </summary>
/// <param name="UserId"></param>
/// <param name="Amount"></param>
/// <param name="Currency"></param>
public sealed record TopUpAccountRequest(
    [property: JsonRequired]
    [property: JsonPropertyName("userId")]
    Guid UserId,

    [property: JsonRequired]
    [property: JsonPropertyName("amount")]
    [property: Range(0.01, 1_000_000)]
    decimal Amount,

    [property: JsonPropertyName("currency")]
    [property: StringLength(3)]
    string Currency = "RUB"
);
