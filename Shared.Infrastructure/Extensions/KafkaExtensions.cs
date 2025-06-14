using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Extensions;

public static class KafkaExtensions
{
    public static IHost EnsureKafkaTopicsCreated(this IHost host, params KafkaTopicConfig[] topics)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IHost>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var bootstrapServers = configuration.GetConnectionString("Kafka") ?? "localhost:9092";
        
        logger.LogInformation("Creating Kafka topics on {BootstrapServers}", bootstrapServers);

        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();

        try
        {
            var topicSpecs = new List<TopicSpecification>();

            foreach (var topic in topics)
            {
                topicSpecs.Add(new TopicSpecification
                {
                    Name = topic.Name,
                    NumPartitions = topic.Partitions,
                    ReplicationFactor = topic.ReplicationFactor,
                    Configs = new Dictionary<string, string>
                    {
                        { "min.insync.replicas", topic.MinInsyncReplicas.ToString() }
                    }
                });
                
                logger.LogInformation("Preparing topic creation: {TopicName}, partitions: {Partitions}, replication: {ReplicationFactor}", 
                    topic.Name, topic.Partitions, topic.ReplicationFactor);
            }

            var retryCount = 0;
            var maxRetries = 5;
            var retryDelayMs = 5000;

            while (retryCount < maxRetries)
            {
                try
                {
                    adminClient.CreateTopicsAsync(topicSpecs).GetAwaiter().GetResult();
                    logger.LogInformation("Kafka topics created successfully");
                    break;
                }
                catch (CreateTopicsException ex)
                {
                    if (ex.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists))
                    {
                        retryCount++;
                        if (retryCount >= maxRetries)
                        {
                            logger.LogError(ex, "Failed to create Kafka topics after {RetryCount} attempts", retryCount);
                            throw;
                        }
                        
                        logger.LogWarning("Failed to create Kafka topics: {Message}. Retrying in {DelayMs}ms...", ex.Message, retryDelayMs);
                        Task.Delay(retryDelayMs).GetAwaiter().GetResult();
                    }
                    else
                    {
                        logger.LogInformation("Some topics already exist, continuing...");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        logger.LogError(ex, "Failed to create Kafka topics after {RetryCount} attempts", retryCount);
                        throw;
                    }
                    
                    logger.LogWarning(ex, "Error creating Kafka topics. Retrying in {DelayMs}ms...", retryDelayMs);
                    Task.Delay(retryDelayMs).GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Kafka topics");
            // В этом случае не выбрасываем исключение, чтобы приложение всё равно запустилось
            // Топики могут быть созданы при первой отправке сообщения или через другие средства
        }

        return host;
    }
}

public class KafkaTopicConfig
{
    public string Name { get; set; }
    public int Partitions { get; set; } = 3;
    public short ReplicationFactor { get; set; } = 1;
    public int MinInsyncReplicas { get; set; } = 1;

    public KafkaTopicConfig(string name)
    {
        Name = name;
    }
} 