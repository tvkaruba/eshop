using System.Text.Json.Serialization;

namespace ApiGateway.Models.PaymentService.Responses;

/// <summary>
/// 
/// </summary>
/// <param name="Success"></param>
/// <param name="Message"></param>
/// <param name="NewBalance"></param>
/// <param name="TransactionId"></param>
public sealed record TopUpAccountResponse(
    [property: JsonRequired]
    [property: JsonPropertyName("success")]
    bool Success,

    [property: JsonRequired]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonRequired]
    [property: JsonPropertyName("newBalance")]
    decimal NewBalance,

    [property: JsonPropertyName("transactionId")]
    Guid? TransactionId = null
);
