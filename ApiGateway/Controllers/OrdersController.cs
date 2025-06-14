using ApiGateway.Models.OrderService;
using ApiGateway.Models.OrderService.Requests;
using ApiGateway.Models.OrderService.Responses;
using Microsoft.AspNetCore.Mvc;
using Proto = Shared.Contracts.Orders;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;

    private readonly Proto.OrdersService.OrdersServiceClient _ordersClient;

    public OrdersController(
        ILogger<OrdersController> logger,
        Proto.OrdersService.OrdersServiceClient ordersClient)
    {
        _logger = logger;
        _ordersClient = ordersClient;
    }

    [HttpPost]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            if (request.Items.Count == 0)
                return TypedResults.BadRequest(new CreateOrderResponse(Success: false, "Заказ должен содержать хотя бы один товар"));

            var grpcRequest = new Proto.CreateOrderRequest
            {
                UserId = request.UserId.ToString(),
            };

            grpcRequest.Items.AddRange(
                request.Items.Select(item => new Proto.OrderItem
                {
                    ProductId = item.ProductId.ToString(),
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    Price = (double)item.Price,
                }));

            var grpcResponse = await _ordersClient.CreateOrderAsync(grpcRequest);

            var response = new CreateOrderResponse(grpcResponse.Success, grpcResponse.Message, Guid.Parse(grpcResponse.OrderId));
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании заказа для пользователя {UserId}", request.UserId);
            return TypedResults.InternalServerError(new CreateOrderResponse(Success: false, "Внутренняя ошибка сервера"));
        }
    }

    [HttpGet("users/{userId}")]
    [ProducesResponseType<GetUserOrdersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<GetUserOrdersResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<GetUserOrdersResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> GetUserOrders(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (page < 1 || pageSize < 1)
                return TypedResults.BadRequest(new GetUserOrdersResponse(
                    Success: false, "Номер страницы и размер страницы должны быть больше 0", Orders: [], TotalCount: 0));

            var grpcRequest = new Proto.GetUserOrdersRequest
            {
                UserId = userId.ToString(),
                Page = page,
                PageSize = pageSize
            };

            var grpcResponse = await _ordersClient.GetUserOrdersAsync(grpcRequest);

            var response = new GetUserOrdersResponse(
                grpcResponse.Success,
                grpcResponse.Message,
                grpcResponse.Orders
                    .Select(order => new OrderDto(
                        Guid.Parse(order.OrderId),
                        Guid.Parse(order.UserId),
                        order.Status.ToString(),
                        order.TotalAmount,
                        order.CreatedAt.ToDateTimeOffset(),
                        order.UpdatedAt.ToDateTimeOffset(),
                        order.Items
                            .Select(item => new OrderItemDto(
                                Guid.Parse(item.ProductId),
                                item.ProductName,
                                item.Quantity,
                                (decimal)item.Price))
                            .ToArray(),
                        Guid.TryParse(order.PaymentTransactionId, out var transactionId) ? transactionId : Guid.Empty))
                    .ToArray(),
                grpcResponse.TotalCount);

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении заказов пользователя {UserId}", userId);
            return TypedResults.InternalServerError(new GetUserOrdersResponse(
                Success: false, "Внутренняя ошибка сервера", Orders: [], TotalCount: 0));
        }
    }

    [HttpGet("{orderId}/status")]
    [ProducesResponseType<GetOrderStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<GetOrderStatusResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<GetOrderStatusResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<GetOrderStatusResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> GetOrderStatus(Guid orderId, [FromQuery] Guid userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId.ToString()))
                return TypedResults.BadRequest(new GetOrderStatusResponse(Success: false, "Требуется указать идентификатор пользователя"));

            var grpcRequest = new Proto.GetOrderStatusRequest
            {
                OrderId = orderId.ToString(),
                UserId = userId.ToString(),
            };

            var grpcResponse = await _ordersClient.GetOrderStatusAsync(grpcRequest);

            if (!grpcResponse.Success)
                return TypedResults.NotFound(new GetOrderStatusResponse(Success: false, grpcResponse.Message));

            var order = grpcResponse.Order;
            var orderResponse = new OrderDto(
                Guid.Parse(order.OrderId),
                Guid.Parse(order.UserId),
                order.Status.ToString(),
                order.TotalAmount,
                order.CreatedAt.ToDateTimeOffset(),
                order.UpdatedAt.ToDateTimeOffset(),
                order.Items
                    .Select(item => new OrderItemDto(
                        Guid.Parse(item.ProductId),
                        item.ProductName,
                        item.Quantity,
                        (decimal)item.Price))
                    .ToArray(),
                Guid.TryParse(order.PaymentTransactionId, out var transactionId) ? transactionId : Guid.Empty);

            var response = new GetOrderStatusResponse(grpcResponse.Success, grpcResponse.Message, orderResponse);
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса заказа {OrderId}", orderId);
            return TypedResults.InternalServerError(new GetOrderStatusResponse(Success: false, "Внутренняя ошибка сервера"));
        }
    }
}
