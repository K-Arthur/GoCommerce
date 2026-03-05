using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using OrderService.Events;

namespace OrderService.Services;

public class RabbitMqPublisher : IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private async Task EnsureConnectionAsync()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "rabbitmq",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest"
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(
                exchange: "order_events",
                type: ExchangeType.Fanout,
                durable: true);

            _logger.LogInformation("Connected to RabbitMQ and declared exchange 'order_events'.");
        }
    }

    public async Task PublishOrderCreatedAsync(OrderCreatedEvent orderEvent)
    {
        try
        {
            await EnsureConnectionAsync();

            var message = JsonSerializer.Serialize(orderEvent);
            var body = Encoding.UTF8.GetBytes(message);

            await _channel!.BasicPublishAsync(
                exchange: "order_events",
                routingKey: string.Empty,
                body: body);

            _logger.LogInformation("Published OrderCreated event for Order ID {OrderId}", orderEvent.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OrderCreated event for Order ID {OrderId}", orderEvent.OrderId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}
