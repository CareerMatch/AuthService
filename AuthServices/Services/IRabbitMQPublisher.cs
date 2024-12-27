namespace AuthServices.Services;

public interface IRabbitMQPublisher
{
    Task SendRegisterMessageAsync(string message);

    Task SendDeleteMessageAsync(string message);
}