using System.Text.Json.Serialization;

namespace ApiGateway.Models.PaymentService.Requests;

/// <summary>
/// 
/// </summary>
/// <param name="UserId"></param>
public sealed record CreateAccountRequest(
    [property: JsonRequired]
    [property: JsonPropertyName("userId")]
    Guid UserId
);
