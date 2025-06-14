using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using Shared.Infrastructure.Interfaces;

namespace OrdersService.Services;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private readonly ILogger<OutboxProcessorService> _logger;

    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);

    public OutboxProcessorService(IServiceProvider serviceProvider, ILogger<OutboxProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orders Outbox Processor Service запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEvents();
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Orders Outbox Processor Service остановлен");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в Orders Outbox Processor Service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ProcessOutboxEvents()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var kafkaProducer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        var unprocessedEvents = await context.OutboxEvents
            .Where(e => !e.IsProcessed && e.RetryCount < 5)
            .OrderBy(e => e.CreatedAt)
            .Take(10)
            .ToListAsync();

        foreach (var outboxEvent in unprocessedEvents)
        {
            try
            {
                _logger.LogDebug("Обработка Orders Outbox события: {EventId}", outboxEvent.Id);

                await kafkaProducer.ProduceAsync("order-events", outboxEvent.Id.ToString(), outboxEvent.EventData);

                outboxEvent.IsProcessed = true;
                outboxEvent.ProcessedAt = DateTime.UtcNow;
                
                await context.SaveChangesAsync();
                
                _logger.LogDebug("Orders Outbox событие обработано успешно: {EventId}", outboxEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке Orders Outbox события: {EventId}", outboxEvent.Id);
                
                outboxEvent.RetryCount++;
                outboxEvent.ErrorMessage = ex.Message;
                
                await context.SaveChangesAsync();
            }
        }
    }
}
