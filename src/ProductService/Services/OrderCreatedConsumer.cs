using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ProductService.Data;
using ProductService.Events;

namespace ProductService.Services;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderCreatedConsumer(
        ILogger<OrderCreatedConsumer> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry connection to RabbitMQ (it may not be ready yet)
        var retryCount = 10;
        while (retryCount > 0 && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                    Password = _configuration["RabbitMQ:Password"] ?? "guest"
                };

                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _channel.ExchangeDeclareAsync(
                    exchange: "order_events",
                    type: ExchangeType.Fanout,
                    durable: true,
                    cancellationToken: stoppingToken);

                var queueDeclareResult = await _channel.QueueDeclareAsync(
                    queue: "product_stock_update",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await _channel.QueueBindAsync(
                    queue: "product_stock_update",
                    exchange: "order_events",
                    routingKey: string.Empty,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Connected to RabbitMQ. Listening on queue 'product_stock_update'.");
                break;
            }
            catch (Exception ex)
            {
                retryCount--;
                _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Retries remaining: {RetryCount}", retryCount);
                if (retryCount == 0)
                {
                    _logger.LogError("Could not connect to RabbitMQ after all retries.");
                    return;
                }
                await Task.Delay(5000, stoppingToken);
            }
        }

        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                if (orderEvent != null)
                {
                    await ProcessOrderCreatedAsync(orderEvent);
                }

                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OrderCreated event.");
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: "product_stock_update",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessOrderCreatedAsync(OrderCreatedEvent orderEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        foreach (var item in orderEvent.Items)
        {
            var product = await context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.StockQuantity -= item.Quantity;
                if (product.StockQuantity < 0) product.StockQuantity = 0;

                _logger.LogInformation(
                    "Updated stock for Product ID {ProductId}: decremented by {Quantity}. New stock: {Stock}",
                    item.ProductId, item.Quantity, product.StockQuantity);
            }
            else
            {
                _logger.LogWarning("Product ID {ProductId} not found for stock update.", item.ProductId);
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Processed OrderCreated event for Order ID {OrderId}", orderEvent.OrderId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}
