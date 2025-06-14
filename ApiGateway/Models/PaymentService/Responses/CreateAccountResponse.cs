using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.PaymentService.Responses;

/// <summary>
/// 
/// </summary>
/// <param name="Success"></param>
/// <param name="Message"></param>
/// <param name="AccountId"></param>
public sealed record CreateAccountResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonPropertyName("accountId")]
    Guid? AccountId = null
);