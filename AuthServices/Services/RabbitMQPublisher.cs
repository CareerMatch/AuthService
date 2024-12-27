using AuthServices.DTOs;

namespace AuthServices.Services;

using System.Text;
using RabbitMQ.Client;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class RabbitMqPublisher : IRabbitMQPublisher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    // Send a registration message
    public async Task SendRegisterMessageAsync(string message)
    {
        await SendMessageAsync(
            message,
            _configuration["RabbitMQ:RegisterExchange"],
            _configuration["RabbitMQ:RegisterQueue"],
            _configuration["RabbitMQ:RegisterRoutingKey"]
        );
    }

    // Send a deletion message
    public async Task SendDeleteMessageAsync(string message)
    {
        await SendMessageAsync(
            message,
            _configuration["RabbitMQ:DeleteExchange"],
            _configuration["RabbitMQ:DeleteQueue"],
            _configuration["RabbitMQ:DeleteRoutingKey"]
        );
    }

    // Core message-sending logic, reused by specific methods
    private async Task SendMessageAsync(string message, string exchange, string queue, string routingKey)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"],
            Port = int.Parse(_configuration["RabbitMQ:Port"]),
            UserName = _configuration["RabbitMQ:Username"],
            Password = _configuration["RabbitMQ:Password"]
        };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declare the exchange
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: "direct", // Direct exchange allows routing based on routing keys
            durable: true // Durable to survive broker restarts
        );

        // Declare the queue (in case it’s not already declared by the consumer)
        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Bind the queue to the exchange with the appropriate routing key
        await channel.QueueBindAsync(
            queue: queue,
            exchange: exchange,
            routingKey: routingKey
        );

        // Encode the message
        var body = Encoding.UTF8.GetBytes(message);

        // Publish the message to the exchange with the routing key
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            body: body
        );

        _logger.LogInformation("Sent message: {Message} to Exchange: {Exchange}, Queue: {Queue}, Routing Key: {RoutingKey}",
            message, exchange, queue, routingKey);
    }
}