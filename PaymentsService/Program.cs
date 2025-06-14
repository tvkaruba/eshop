using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Services;
using Serilog;
using Serilog.Events;
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

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<ICacheService, GarnetCacheService>();

builder.Services.AddHostedService<OutboxProcessorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await context.Database.MigrateAsync();
}

// Создание топиков Kafka
app.EnsureKafkaTopicsCreated(
    new KafkaTopicConfig("payment-events"),
    new KafkaTopicConfig("order-events")
);

app.MapGrpcService<PaymentsGrpcService>();

app.MapGet("/health", () => "Healthy");

app.Run();
