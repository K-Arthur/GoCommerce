using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ShippingService.Data;
using ShippingService.Events;

namespace ShippingService.Services;

public class OrderCancelledConsumer : BackgroundService
{
    private readonly ILogger<OrderCancelledConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderCancelledConsumer(
        ILogger<OrderCancelledConsumer> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                    exchange: "order_cancelled_events",
                    type: ExchangeType.Fanout,
                    durable: true,
                    cancellationToken: stoppingToken);

                await _channel.QueueDeclareAsync(
                    queue: "shipment_cancellation",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await _channel.QueueBindAsync(
                    queue: "shipment_cancellation",
                    exchange: "order_cancelled_events",
                    routingKey: string.Empty,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Connected to RabbitMQ. Listening on queue 'shipment_cancellation'.");
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
                var orderEvent = JsonSerializer.Deserialize<OrderCancelledEvent>(message);

                if (orderEvent != null)
                {
                    await ProcessOrderCancelledAsync(orderEvent);
                }

                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OrderCancelled event.");
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: "shipment_cancellation",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessOrderCancelledAsync(OrderCancelledEvent orderEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();

        var shipment = await context.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderEvent.OrderId);
        if (shipment != null)
        {
            shipment.Status = "Cancelled";
            await context.SaveChangesAsync();
            _logger.LogInformation(
                "Cancelled shipment for Order ID {OrderId} (Shipment ID {ShipmentId})",
                orderEvent.OrderId, shipment.Id);
        }
        else
        {
            _logger.LogInformation(
                "No shipment found for cancelled Order ID {OrderId} — nothing to update.",
                orderEvent.OrderId);
        }
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
