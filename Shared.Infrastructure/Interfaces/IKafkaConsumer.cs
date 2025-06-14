namespace Shared.Infrastructure.Interfaces;

public interface IKafkaConsumer : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public interface IKafkaConsumer<T> : IKafkaConsumer
{
    event Func<T, Task> OnMessageReceived;
}
