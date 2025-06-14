using Serilog;
using Serilog.Events;
using Shared.Contracts.Orders;
using Shared.Contracts.Payments;

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "eShop API Gateway", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("http://localhost:5004", "http://blazor-frontend:5004", "http://localhost:8081")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddGrpcClient<PaymentsService.PaymentsServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration.GetConnectionString("PaymentsService") ?? "http://payments-service:5001");
});
builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration.GetConnectionString("OrdersService") ?? "http://orders-service:5002");
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "eShop API Gateway v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowBlazor");
app.UseRouting();

app.MapControllers();

app.MapReverseProxy();

app.MapGet("/health", () => "Healthy");

app.Run();
