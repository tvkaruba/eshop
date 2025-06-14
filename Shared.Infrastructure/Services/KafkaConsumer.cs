using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Interfaces;
using System.Text.Json;

namespace Shared.Infrastructure.Services;

public class KafkaConsumer<T> : IKafkaConsumer<T>
{
    private readonly IConsumer<string, string> _consumer;

    private readonly ILogger<KafkaConsumer<T>> _logger;

    private readonly string _topic;

    private readonly CancellationTokenSource _cancellationTokenSource;

    public event Func<T, Task>? OnMessageReceived;

    public KafkaConsumer(IConfiguration configuration, ILogger<KafkaConsumer<T>> logger, string topic, string groupId)
    {
        _logger = logger;
        _topic = topic;
        _cancellationTokenSource = new CancellationTokenSource();

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092",
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Ручное подтверждение для exactly once
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 300000
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Kafka Consumer запущен для топика: {Topic}", _topic);

        await Task.Run(() => ConsumeLoop(cancellationToken), cancellationToken);
    }

    private void ConsumeLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    
                    if (consumeResult?.Message != null)
                    {
                        var message = JsonSerializer.Deserialize<T>(consumeResult.Message.Value);
                        if (message != null && OnMessageReceived != null)
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    await OnMessageReceived(message);
                                    _consumer.Commit(consumeResult);
                                    _logger.LogInformation("Сообщение обработано успешно. Offset: {Offset}", consumeResult.Offset);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Ошибка при обработке сообщения. Offset: {Offset}", consumeResult.Offset);
                                    // Здесь можно добавить логику повторной попытки или dead letter queue
                                }
                            }, cancellationToken);
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Ошибка при получении сообщения из Kafka");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka Consumer остановлен");
        }
        finally
        {
            _consumer.Close();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _consumer?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
