using Grpc.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OrdersService.Data;
using OrdersService.Hubs;
using Shared.Contracts.Orders;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Models;

namespace OrdersService.Services;

public class OrdersGrpcService : Shared.Contracts.Orders.OrdersService.OrdersServiceBase
{
    private readonly OrdersDbContext _context;

    private readonly IKafkaProducer _kafkaProducer;

    private readonly ICacheService _cacheService;

    private readonly IHubContext<OrderStatusHub> _hubContext;

    private readonly ILogger<OrdersGrpcService> _logger;

    public OrdersGrpcService(
        OrdersDbContext context,
        IKafkaProducer kafkaProducer,
        ICacheService cacheService,
        IHubContext<OrderStatusHub> hubContext,
        ILogger<OrdersGrpcService> logger)
    {
        _context = context;
        _kafkaProducer = kafkaProducer;
        _cacheService = cacheService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public override async Task<CreateOrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Создание заказа для пользователя: {UserId}", request.UserId);

            if (!request.Items.Any())
            {
                return new CreateOrderResponse
                {
                    Success = false,
                    Message = "Заказ должен содержать хотя бы один товар"
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var order = new Models.Order
                {
                    UserId = request.UserId,
                    Status = Models.OrderStatus.Pending,
                    TotalAmount = (decimal)request.Items.Sum(i => i.Price),
                };

                foreach (var item in request.Items)
                {
                    var orderItem = new Models.OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = (decimal)item.Price
                    };
                    
                    order.Items.Add(orderItem);
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var orderCreatedEvent = new OrderCreatedEvent
                {
                    OrderId = order.Id.ToString(),
                    UserId = order.UserId,
                    TotalAmount = (double)order.TotalAmount,
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    Items = order.Items.Select(i => new OrderItemEvent
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        Price = (double)i.Price
                    }).ToList()
                };

                var outboxEvent = new OutboxEvent
                {
                    EventType = nameof(OrderCreatedEvent),
                    EventData = JsonConvert.SerializeObject(orderCreatedEvent)
                };

                _context.OutboxEvents.Add(outboxEvent);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients.Group($"user_{request.UserId}")
                    .SendAsync("OrderStatusChanged", new
                    {
                        OrderId = order.Id.ToString(),
                        Status = order.Status.ToString(),
                        Message = "Заказ создан"
                    });

                _logger.LogInformation("Заказ создан успешно. OrderId: {OrderId}", order.Id);

                return new CreateOrderResponse
                {
                    Success = true,
                    Message = "Заказ создан успешно",
                    OrderId = order.Id.ToString()
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании заказа. UserId: {UserId}", request.UserId);
            return new CreateOrderResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    public override async Task<GetUserOrdersResponse> GetUserOrders(GetUserOrdersRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Получение заказов пользователя: {UserId}", request.UserId);

            var pageSize = request.PageSize > 0 ? request.PageSize : 10;
            var page = request.Page > 0 ? request.Page : 1;
            var skip = (page - 1) * pageSize;

            var query = _context.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == request.UserId)
                .OrderByDescending(o => o.CreatedAt);

            var totalCount = await query.CountAsync();
            var orders = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var response = new GetUserOrdersResponse
            {
                Success = true,
                Message = "Заказы получены успешно",
                TotalCount = totalCount
            };

            foreach (var order in orders)
            {
                var orderDto = new Shared.Contracts.Orders.Order
                {
                    OrderId = order.Id.ToString(),
                    UserId = order.UserId,
                    Status = (Shared.Contracts.Orders.OrderStatus)(int)order.Status,
                    TotalAmount = (double)order.TotalAmount,
                    CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(order.CreatedAt.ToUniversalTime()),
                    UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(order.UpdatedAt.ToUniversalTime()),
                    PaymentTransactionId = order.PaymentTransactionId ?? string.Empty
                };

                foreach (var item in order.Items)
                {
                    orderDto.Items.Add(new Shared.Contracts.Orders.OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = (double)item.Price
                    });
                }

                response.Orders.Add(orderDto);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении заказов. UserId: {UserId}", request.UserId);
            return new GetUserOrdersResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    public override async Task<GetOrderStatusResponse> GetOrderStatus(GetOrderStatusRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Получение статуса заказа: {OrderId}", request.OrderId);


            var cacheKey = $"order:{request.OrderId}";
            var cachedOrder = await _cacheService.GetAsync<Models.Order>(cacheKey);
            
            if (cachedOrder != null && cachedOrder.UserId == request.UserId)
            {
                return CreateOrderStatusResponse(cachedOrder, true, "Статус заказа получен успешно");
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == Guid.Parse(request.OrderId) && o.UserId == request.UserId);

            if (order == null)
            {
                return new GetOrderStatusResponse
                {
                    Success = false,
                    Message = "Заказ не найден"
                };
            }


            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(5));

            return CreateOrderStatusResponse(order, true, "Статус заказа получен успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении статуса заказа. OrderId: {OrderId}", request.OrderId);
            return new GetOrderStatusResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    private GetOrderStatusResponse CreateOrderStatusResponse(Models.Order order, bool success, string message)
    {
        var orderDto = new Shared.Contracts.Orders.Order
        {
            OrderId = order.Id.ToString(),
            UserId = order.UserId,
            Status = (Shared.Contracts.Orders.OrderStatus)(int)order.Status,
            TotalAmount = (double)order.TotalAmount,
            CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(order.CreatedAt.ToUniversalTime()),
            UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(order.UpdatedAt.ToUniversalTime()),
            PaymentTransactionId = order.PaymentTransactionId ?? string.Empty
        };

        foreach (var item in order.Items)
        {
            orderDto.Items.Add(new Shared.Contracts.Orders.OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = (double)item.Price
            });
        }

        return new GetOrderStatusResponse
        {
            Success = success,
            Message = message,
            Order = orderDto
        };
    }
}
