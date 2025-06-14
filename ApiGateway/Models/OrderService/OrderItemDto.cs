using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGateway.Models.OrderService;

/// <summary>
/// 
/// </summary>
/// <param name="ProductId"></param>
/// <param name="ProductName"></param>
/// <param name="Quantity"></param>
/// <param name="Price"></param>
public sealed record OrderItemDto(
    [property: JsonRequired]
    [property: JsonPropertyName("productId")]
    Guid ProductId,

    [property: JsonRequired]
    [property: JsonPropertyName("productName")]
    [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters")]
    string ProductName,

    [property: JsonRequired]
    [property: JsonPropertyName("quantity")]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1-1000")]
    int Quantity,

    [property: JsonRequired]
    [property: JsonPropertyName("price")]
    decimal Price
);
