using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Hubs;
using OrdersService.Services;
using Serilog;
using Serilog.Events;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:5341"));

builder.Services.AddGrpc();
builder.Services.AddSignalR();

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();

builder.Services.AddSingleton<IKafkaConsumer<PaymentProcessedEvent>>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<KafkaConsumer<PaymentProcessedEvent>>>();
    return new KafkaConsumer<PaymentProcessedEvent>(configuration, logger, "payment-events", "orders-service-group");
});

builder.Services.AddHostedService<OutboxProcessorService>();
builder.Services.AddHostedService<PaymentEventProcessor>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await context.Database.MigrateAsync();
}

app.EnsureKafkaTopicsCreated(
    new KafkaTopicConfig("payment-events"),
    new KafkaTopicConfig("order-events")
);

app.MapGrpcService<OrdersGrpcService>();
app.MapHub<OrderStatusHub>("/orderStatusHub");

app.MapGet("/health", () => "Healthy");

app.Run();
