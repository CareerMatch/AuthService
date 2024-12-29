using System.Text;
using System.Text.Json;
using AuthServices.Config;
using AuthServices.DTOs;
using AuthServices.Models;
using AuthServices.Repository;
using AuthServices.Services;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;
using Xunit;

namespace AuthServices.Test.IntegrationTests
{
    public class AuthServicesIntegrationTest : IAsyncLifetime
    {
        private readonly RabbitMqContainer _rabbitMqContainer;
        private readonly MongoDbContainer _mongoDbContainer;
        private IConnection _connection;
        private IChannel _channel;
        private MongoDbContext _mongoDbContext;

        public AuthServicesIntegrationTest()
        {
            // Initialize RabbitMQ Test Container
            _rabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .WithPortBinding(5672)
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();

            // Initialize MongoDB Test Container
            _mongoDbContainer = new MongoDbBuilder()
                .WithImage("mongo:6.0")
                .WithPortBinding(27017)
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .Build();
        }

        public async Task InitializeAsync()
        {
            // Start RabbitMQ and MongoDB containers
            await _rabbitMqContainer.StartAsync();
            await _mongoDbContainer.StartAsync();

            // Set up RabbitMQ connection and channel
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = _rabbitMqContainer.GetMappedPublicPort(5672),
                UserName = "guest",
                Password = "guest"
            };
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declare queues and exchanges
            await _channel.ExchangeDeclareAsync("auth_to_user", "direct", durable: true);
            await _channel.QueueDeclareAsync("user_register", durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync("user_register", "auth_to_user", "user.register");

            _channel.ExchangeDeclareAsync("auth_to_user", "direct", durable: true);
            _channel.QueueDeclareAsync("user_register", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBindAsync("user_register", "auth_to_user", "user.register");

            _channel.ExchangeDeclareAsync("user_to_auth", "direct", durable: true);
            _channel.QueueDeclareAsync("user_delete", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBindAsync("user_delete", "user_to_auth", "user.delete");


            // Set up MongoDbContext
            var mongoSettings = new MongoDbSettings
            {
                ConnectionString = _mongoDbContainer.GetConnectionString(),
                DatabaseName = "TestDb"
            };
            var mongoOptions = Options.Create(mongoSettings);
            _mongoDbContext = new MongoDbContext(mongoOptions);
        }

        [Fact]
        public async Task RegisterUser_MessageFlow_Success()
        {
            // Arrange: Set up AuthService and RabbitMQ Publisher
            var authRepository =
                new AuthRepository(_mongoDbContext); // Use actual repository with Testcontainers MongoDB
            var rabbitMqPublisher =
                new RabbitMqPublisher(GetRabbitMqConfiguration(), NullLogger<RabbitMqPublisher>.Instance);
            var authService = new AuthService(authRepository, Options.Create(new JwtSettings()), rabbitMqPublisher);

            // Act: Simulate user registration
            var registerDto = new RegisterDTO
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "test@example.com",
                DateOfBirth = new DateTime(1990, 1, 1),
                Password = "Test123!"
            };
            await authService.RegisterAsync(registerDto);

            // Wait for message processing
            await Task.Delay(1000);

            // Step 1: Assert that the user was added to MongoDB
            var savedUser = await _mongoDbContext.AuthModels
                .Find(u => u.Email == registerDto.Email)
                .FirstOrDefaultAsync();

            Assert.NotNull(savedUser);
            Assert.Equal(registerDto.Email, savedUser.Email);

            // Step 2: Assert that the RabbitMQ message is sent correctly
            var consumer = new AsyncEventingBasicConsumer(_channel);
            string receivedMessage = null;
            consumer.ReceivedAsync += async (model, ea) =>
            {
                receivedMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            await _channel.BasicConsumeAsync(
                queue: "user_register",
                autoAck: false,
                consumer: consumer
            );

            await Task.Delay(500); // Wait for message to be consumed

            Assert.NotNull(receivedMessage);

            // Deserialize the message into a dictionary
            var deserializedMessage = JsonSerializer.Deserialize<Dictionary<string, object>>(receivedMessage);

            Assert.NotNull(deserializedMessage);
            Assert.Equal("John", deserializedMessage["FirstName"].ToString());
            Assert.Equal("Doe", deserializedMessage["LastName"].ToString());
            Assert.Equal("User", deserializedMessage["Role"].ToString());
        }

        [Fact]
        public async Task RegisterUser_MessageFlow_Failure_InvalidMessageFormat()
        {
            // Arrange: Set up RabbitMQ Publisher
            var authRepository = new AuthRepository(_mongoDbContext);
            var rabbitMqPublisher =
                new RabbitMqPublisher(GetRabbitMqConfiguration(), NullLogger<RabbitMqPublisher>.Instance);
            var authService = new AuthService(authRepository, Options.Create(new JwtSettings()), rabbitMqPublisher);

            // Act: Send an invalid message
            var invalidMessage = JsonSerializer.Serialize(new
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1) // Missing UserId and Role
            });

            await _channel.BasicPublishAsync(
                exchange: "auth_to_user",
                routingKey: "user.register",
                body: Encoding.UTF8.GetBytes(invalidMessage)
            );

            await Task.Delay(1000); // Wait for message processing

            // Assert: No user should be added to the database
            var savedUser = await _mongoDbContext.AuthModels
                .Find(u => u.Email == "test@example.com")
                .FirstOrDefaultAsync();

            Assert.Null(savedUser);
        }

        [Fact]
        public async Task DeleteUser_MessageFlow_Success()
        {
            // Arrange: Set up AuthService, RabbitMQ Publisher, and Repository
            var authRepository =
                new AuthRepository(_mongoDbContext); // Use actual repository with Testcontainers MongoDB
            var rabbitMqPublisher =
                new RabbitMqPublisher(GetRabbitMqConfiguration(), NullLogger<RabbitMqPublisher>.Instance);
            var authService = new AuthService(authRepository, Options.Create(new JwtSettings()), rabbitMqPublisher);

            // Add a user to the database
            var userId = Guid.NewGuid();
            var user = new AuthModel
            {
                UserId = userId,
                Email = "delete_test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
            };
            await authRepository.AddUserAsync(user);

            // Act: Call DeleteUserAsync
            await authService.DeleteUserAsync(userId);

            // Wait for RabbitMQ message processing
            await Task.Delay(1000);

            // Assert: Verify the user was removed from the database
            var deletedUser = await _mongoDbContext.AuthModels
                .Find(u => u.UserId == userId)
                .FirstOrDefaultAsync();
            Assert.Null(deletedUser);

            // Assert: Verify RabbitMQ message was sent
            var consumer = new AsyncEventingBasicConsumer(_channel);
            string receivedMessage = null;
            consumer.ReceivedAsync += async (model, ea) =>
            {
                receivedMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            await _channel.BasicConsumeAsync(
                queue: "user_delete",
                autoAck: false,
                consumer: consumer
            );

            await Task.Delay(500);

            Assert.NotNull(receivedMessage);

            // Deserialize and verify the message
            var deserializedMessage = JsonSerializer.Deserialize<Dictionary<string, object>>(receivedMessage);
            Assert.NotNull(deserializedMessage);
            Assert.Equal(userId.ToString(), deserializedMessage["UserId"].ToString());
        }

        public async Task DisposeAsync()
        {
            // Clean up RabbitMQ resources
            await _channel.CloseAsync();
            await _connection.CloseAsync();
            await _rabbitMqContainer.StopAsync();

            // Clean up MongoDB resources
            await _mongoDbContainer.StopAsync();
        }

        private IConfiguration GetRabbitMqConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "RabbitMQ:Host", "localhost" },
                    { "RabbitMQ:Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString() },
                    { "RabbitMQ:Username", "guest" },
                    { "RabbitMQ:Password", "guest" },
                    { "RabbitMQ:RegisterExchange", "auth_to_user" },
                    { "RabbitMQ:RegisterQueue", "user_register" },
                    { "RabbitMQ:RegisterRoutingKey", "user.register" },
                    { "RabbitMQ:DeleteExchange", "user_to_auth" },
                    { "RabbitMQ:DeleteQueue", "user_delete" },
                    { "RabbitMQ:DeleteRoutingKey", "user.delete" }
                })
                .Build();
        }
    }
}