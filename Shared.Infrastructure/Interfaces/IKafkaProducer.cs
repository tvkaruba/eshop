namespace Shared.Infrastructure.Interfaces;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, string key, T message);

    Task ProduceAsync<T>(string topic, T message);
}
