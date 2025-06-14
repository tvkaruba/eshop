using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OrdersService.Data;
using OrdersService.Hubs;
using OrdersService.Models;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Models;

namespace OrdersService.Services;

public class PaymentEventProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private readonly ILogger<PaymentEventProcessor> _logger;

    public PaymentEventProcessor(IServiceProvider serviceProvider, ILogger<PaymentEventProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Event Processor запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                var kafkaConsumer = scope.ServiceProvider.GetRequiredService<IKafkaConsumer<PaymentProcessedEvent>>();
                
                kafkaConsumer.OnMessageReceived += async (paymentEvent) =>
                {
                    await ProcessPaymentEvent(paymentEvent);
                };

                await kafkaConsumer.StartAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Payment Event Processor остановлен");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в Payment Event Processor");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ProcessPaymentEvent(PaymentProcessedEvent paymentEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<OrderStatusHub>>();

        try
        {
            _logger.LogInformation("Обработка события оплаты для заказа: {OrderId}", paymentEvent.OrderId);


            var existingInboxEvent = await context.InboxEvents
                .FirstOrDefaultAsync(e => e.IdempotencyKey == paymentEvent.IdempotencyKey);

            if (existingInboxEvent != null && existingInboxEvent.IsProcessed)
            {
                _logger.LogInformation("Событие уже было обработано: {IdempotencyKey}", paymentEvent.IdempotencyKey);
                return;
            }

            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var inboxEvent = existingInboxEvent;

                if (inboxEvent == null)
                {
                    inboxEvent = new InboxEvent
                    {
                        EventType = nameof(PaymentProcessedEvent),
                        EventData = JsonConvert.SerializeObject(paymentEvent),
                        IdempotencyKey = paymentEvent.IdempotencyKey
                    };

                    context.InboxEvents.Add(inboxEvent);
                }


                var order = await context.Orders
                    .FirstOrDefaultAsync(o => o.Id == Guid.Parse(paymentEvent.OrderId));

                if (order == null)
                {
                    _logger.LogWarning("Заказ не найден: {OrderId}", paymentEvent.OrderId);
                    inboxEvent.IsProcessed = true;
                    inboxEvent.ProcessedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return;
                }


                if (paymentEvent.Success)
                {
                    order.Status = OrderStatus.Paid;
                    order.PaymentTransactionId = paymentEvent.TransactionId;
                    _logger.LogInformation("Заказ оплачен успешно: {OrderId}", paymentEvent.OrderId);
                }
                else
                {
                    order.Status = OrderStatus.PaymentFailed;
                    _logger.LogInformation("Оплата заказа не удалась: {OrderId}, причина: {Error}", 
                        paymentEvent.OrderId, paymentEvent.ErrorMessage);
                }

                order.UpdatedAt = DateTime.UtcNow;

                inboxEvent.IsProcessed = true;
                inboxEvent.ProcessedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                await cacheService.RemoveAsync($"order:{paymentEvent.OrderId}");

                await hubContext.Clients.Group($"user_{paymentEvent.UserId}")
                    .SendAsync("OrderStatusChanged", new
                    {
                        OrderId = paymentEvent.OrderId,
                        Status = order.Status.ToString(),
                        Message = paymentEvent.Success 
                            ? "Заказ оплачен успешно" 
                            : $"Ошибка оплаты: {paymentEvent.ErrorMessage}"
                    });

                _logger.LogInformation("Событие оплаты обработано успешно: {OrderId}", paymentEvent.OrderId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)

        {
            _logger.LogError(ex, "Ошибка при обработке события оплаты: {OrderId}", paymentEvent.OrderId);
            throw;
        }
    }
}
