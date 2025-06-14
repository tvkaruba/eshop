using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Shared.Infrastructure.Services;

public class GarnetCacheService : ICacheService, IDisposable
{
    private readonly IDatabase _database;

    private readonly IConnectionMultiplexer _connection;

    private readonly ILogger<GarnetCacheService> _logger;

    public GarnetCacheService(IConfiguration configuration, ILogger<GarnetCacheService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration.GetConnectionString("Garnet") ?? "localhost:6379";
        
        var configOptions = ConfigurationOptions.Parse(connectionString);
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectRetry = 3;
        configOptions.ConnectTimeout = 5000;
        
        _connection = ConnectionMultiplexer.Connect(configOptions);
        _database = _connection.GetDatabase();
        
        _logger.LogInformation("Подключение к Garnet установлено: {ConnectionString}", connectionString);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return default(T);

            var json = value.ToString();
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении значения из кэша. Key: {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiration);
            _logger.LogDebug("Значение сохранено в кэш. Key: {Key}, Expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении значения в кэш. Key: {Key}", key);
            throw;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Значение удалено из кэша. Key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении значения из кэша. Key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке существования ключа в кэше. Key: {Key}", key);
            return false;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
